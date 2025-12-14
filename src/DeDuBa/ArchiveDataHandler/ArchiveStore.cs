using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.BZip2;
using UtilitiesLibrary;

namespace ArchiveDataHandler;

/// <summary>
///     Implementation of content-addressable archive storage with automatic deduplication.
///     Uses SHA-512 hashing and BZip2 compression. Automatically reorganizes storage directories
///     when they exceed configurable entry thresholds.
/// </summary>
public sealed class ArchiveStore : IArchiveStore
{
    private static readonly object _instanceLock = new();
    private static IArchiveStore? _instance;
    private readonly ConcurrentDictionary<string, string> _arlist = new();
    private readonly IBackupConfig _config;
    private readonly Action<string> _log;
    private readonly ILogging _logger;
    private readonly ConcurrentDictionary<string, HashSet<string>> _preflist = new();
    private readonly object _reorgLock = new();
    private readonly ConcurrentDictionary<string, long> _stats = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ArchiveStore" /> class.
    /// </summary>
    /// <param name="config">Configuration settings for the archive.</param>
    /// <param name="logger">Optional logger instance for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config" /> is null.</exception>
    public ArchiveStore(IBackupConfig config, ILogging? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? UtilitiesLogger.Instance;
        _log = s =>
        {
            if (_config.Verbose)
                _logger.ConWrite(s);
        };
        try
        {
            Directory.CreateDirectory(_config.DataPath);
        }
        catch (Exception ex)
        {
            _logger.Error(_config.DataPath, nameof(Directory.CreateDirectory), ex);
            throw;
        }

        _instance = this;
    }

    /// <summary>
    ///     Default singleton instance of an ArchiveStore configured from utilities.
    ///     Constructed lazily using `BackupConfig.FromUtilities()` and the default logger.
    /// </summary>
    public static IArchiveStore Instance
    {
        get
        {
            if (_instance is not null)
                return _instance;
            throw new InvalidOperationException("ArchiveStore instance not initialized.");
        }
    }

    /// <inheritdoc />
    public string DataPath => _config.DataPath;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Arlist => _arlist;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Preflist =>
        _preflist.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyCollection<string>)kvp.Value.ToList().AsReadOnly());

    /// <inheritdoc />
    public IReadOnlyDictionary<string, long> Stats => _stats;

    /// <inheritdoc />
    public long PackSum { get; private set; }

    /// <inheritdoc />
    public void BuildIndex()
    {
        var root = _config.DataPath;
        if (!Directory.Exists(root))
            return;

        foreach (var entry in Directory.GetFileSystemEntries(root).OrderBy(e => e))
            MkarlistInternal(entry);
    }

    /// <inheritdoc />
    public string? GetTargetPathForHash(string hash)
    {
        if (_config.Verbose)
            _log.Invoke($"GetTargetPathForHash: {hash}");

        if (_arlist.TryGetValue(hash, out var existingPrefix))
        {
            var fPath = Path.Combine(_config.DataPath, existingPrefix, hash);
            try
            {
                if (File.Exists(fPath))
                    PackSum += new FileInfo(fPath).Length;
            }
            catch
            {
                // ignore
            }

            return null;
        }

        var prefix = hash;
        var prefixList = Regex.Split(prefix, "(..)").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        prefixList.RemoveAt(prefixList.Count - 1);

        while (prefixList.Count > 0)
        {
            prefix = string.Join("/", prefixList);
            if (_preflist.ContainsKey(prefix))
                break;
            prefixList.RemoveAt(prefixList.Count - 1);
        }

        if (prefixList.Count == 0)
            prefix = string.Join("/", prefixList);

        var list = _preflist.ContainsKey(prefix) ? _preflist[prefix].ToList() : [];
        var nlist = list.Count;

        if (nlist > _config.PrefixSplitThreshold)
        {
            lock (_reorgLock)
            {
                list = _preflist.ContainsKey(prefix) ? [.. _preflist[prefix]] : [];
                nlist = list.Count;
                if (nlist > _config.PrefixSplitThreshold)
                {
                    if (_config.Verbose)
                        _log.Invoke($"*** reorganizing '{prefix}' [{nlist} entries]");

                    var depth = prefixList.Count;
                    var plen = 2 * depth;
                    var newDirs = new HashSet<string>(list.Where(f => f.EndsWith("/")));

                    for (var n = 0x00; n <= 0xff; n++)
                    {
                        var dir = $"{n:x2}";
                        var de = $"{dir}/";
                        if (!newDirs.Contains(de))
                        {
                            CreateDirectoryWithLogging(Path.Combine(_config.DataPath, prefix, dir));
                            newDirs.Add(de);
                            _preflist.TryAdd(JoinPrefix(prefix, dir), []);
                        }
                    }

                    foreach (var f in list)
                    {
                        if (f.EndsWith("/"))
                            continue;
                        if (f.Length < plen + 2)
                            continue;
                        var dir = f.Substring(plen, 2);
                        var dirPath = Path.Combine(_config.DataPath, prefix, dir);
                        Directory.CreateDirectory(dirPath);
                        var from = Path.Combine(_config.DataPath, prefix, f);
                        var to = Path.Combine(_config.DataPath, prefix, dir, f);
                        try
                        {
                            if (File.Exists(from))
                                File.Move(from, to);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[GTPFH] {from} -> {to}", nameof(File.Move), ex);
                            continue;
                        }

                        var newpfx = JoinPrefix(prefix, dir);
                        _arlist[f] = newpfx;
                        var set = _preflist.GetOrAdd(newpfx, p => []);
                        lock (set)
                        {
                            set.Add(f);
                        }
                    }

                    _preflist[prefix] = newDirs;
                }
            }

            var depth2 = prefixList.Count;
            var plen2 = 2 * depth2;
            var dir2 = hash.Substring(plen2, 2);
            prefix = JoinPrefix(prefix, dir2);
        }

        _arlist[hash] = prefix;
        var pset = _preflist.GetOrAdd(prefix, _ => []);
        lock (pset)
        {
            pset.Add(hash);
        }

        return Path.Combine(_config.DataPath, prefix, hash);
    }

    /// <inheritdoc />
    public string SaveData(ReadOnlySpan<byte> data)
    {
        var hashBytes = SHA512.HashData(data);
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        var outFile = GetTargetPathForHash(hash);
        if (outFile != null)
        {
            _stats.AddOrUpdate("saved_blocks", 1, (_, v) => v + 1);
            var dataLen = data.Length;
            _stats.AddOrUpdate("saved_bytes", dataLen, (_, v) => v + dataLen);

            try
            {
                var directory = Path.GetDirectoryName(outFile);
                if (!string.IsNullOrEmpty(directory))
                    CreateDirectoryWithLogging(directory);

                using (var outputStream = File.Create(outFile))
                using (var bzip2 = new BZip2OutputStream(outputStream))
                {
                    bzip2.Write(data.ToArray(), 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(outFile, nameof(BZip2OutputStream), ex);
                try
                {
                    PackSum += new FileInfo(outFile).Length;
                }
                catch { }

                return hash;
            }

            try
            {
                PackSum += new FileInfo(outFile).Length;
            }
            catch { }

            if (_config.Verbose)
                _log.Invoke(hash);
        }
        else
        {
            _stats.AddOrUpdate("duplicate_blocks", 1, (_, v) => v + 1);
            var dataLen2 = data.Length;
            _stats.AddOrUpdate("duplicate_bytes", dataLen2, (_, v) => v + dataLen2);
            if (_config.Verbose)
                _log.Invoke($"{hash} already exists");
        }

        return hash;
    }

    /// <inheritdoc />
    public List<string> SaveStream(Stream fileStream, long size, string tag, Action<long>? progress = null)
    {
        var hashes = new List<string>();
        var total = size;
        var processed = 0L;
        var bufferSize = (int)Math.Min(_config.ChunkSize, size <= 0 ? _config.ChunkSize : size);
        var buffer = new byte[bufferSize];

        while (size > 0)
        {
            var toRead = (int)Math.Min(bufferSize, size);
            var read = fileStream.Read(buffer, 0, toRead);
            if (read == 0)
                break;
            var span = new ReadOnlySpan<byte>(buffer, 0, read);
            var h = SaveData(span);
            hashes.Add(h);

            size -= read;
            processed += read;
            progress?.Invoke(read);
        }

        return hashes;
    }

    /// <summary>
    ///     Recursively scans a directory entry and populates the hash and prefix indexes.
    ///     Processes hex-prefixed directories and hash files, building the internal tracking structures.
    /// </summary>
    /// <param name="entry">Absolute path to the directory or file entry to process.</param>
    private void MkarlistInternal(string entry)
    {
        if (entry == _config.DataPath)
        {
            if (_config.Verbose)
                _log.Invoke($"+ {entry}");
            try
            {
                foreach (var e in Directory.GetFileSystemEntries(entry).OrderBy(x => x))
                    MkarlistInternal(e);
            }
            catch (Exception ex)
            {
                _logger.Error(entry, nameof(Directory.GetFileSystemEntries), ex);
            }

            return;
        }

        var rel = Path.GetRelativePath(_config.DataPath, entry).Replace(Path.DirectorySeparatorChar, '/');
        var prefix = "";
        var file = rel;
        var idx = rel.LastIndexOf('/');
        if (idx >= 0)
        {
            prefix = rel.Substring(0, idx);
            file = rel.Substring(idx + 1);
        }

        if (Regex.IsMatch(file, "^[0-9a-f][0-9a-f]$"))
        {
            if (_config.Verbose)
                _log.Invoke($"+ {entry}:{prefix}:{file}");
            var set = _preflist.GetOrAdd(prefix, _ => []);
            lock (set)
            {
                set.Add($"{file}/");
            }

            try
            {
                foreach (var e in Directory.GetFileSystemEntries(entry).OrderBy(x => x))
                    MkarlistInternal(e);
            }
            catch (Exception ex)
            {
                _logger.Error(entry, nameof(Directory.GetFileSystemEntries), ex);
            }
        }
        else if (Regex.IsMatch(file, "^[0-9a-f]+$"))
        {
            _arlist[file] = prefix;
            var set = _preflist.GetOrAdd(prefix, _ => []);
            lock (set)
            {
                set.Add(file);
            }
        }
        else
        {
            Utilities.Warn($"Bad entry in archive: {entry}");
        }
    }

    /// <summary>
    ///     Joins a storage prefix with a child segment, ensuring no leading slash.
    ///     Guarantees the result stays relative for safe <see cref="M:System.IO.Path.Combine(System.String,System.String)" />
    ///     operations.
    /// </summary>
    /// <param name="prefix">Parent prefix path (e.g., "aa/bb" or empty string).</param>
    /// <param name="segment">Child segment to append (e.g., "cc").</param>
    /// <returns>Joined path without leading slash (e.g., "aa/bb/cc" or "cc").</returns>
    private static string JoinPrefix(string prefix, string segment)
    {
        return string.IsNullOrEmpty(prefix) ? segment : $"{prefix}/{segment}";
    }

    /// <summary>
    ///     Creates a directory and optionally logs creation in blue text if verbose mode is enabled.
    ///     Idempotent operation (succeeds if directory already exists).
    /// </summary>
    /// <param name="path">Absolute path to the directory to create.</param>
    private void CreateDirectoryWithLogging(string path)
    {
        Directory.CreateDirectory(path);
        if (_config.Verbose)
        {
            const string blue = "\u001b[34m";
            const string reset = "\u001b[0m";
            _log.Invoke($"{blue}Created directory: {path}{reset}");
        }
    }
}

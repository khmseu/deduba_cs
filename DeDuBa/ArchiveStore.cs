using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.BZip2;
using UtilitiesLibrary;

namespace DeDuBa;

public sealed class ArchiveStore : IArchiveStore
{
    private readonly ConcurrentDictionary<string, string> _arlist = new();
    private readonly BackupConfig _config;
    private readonly Action<string>? _log;
    private readonly ConcurrentDictionary<string, HashSet<string>> _preflist = new();
    private readonly object _reorgLock = new();
    private readonly ConcurrentDictionary<string, long> _stats = new();

    public ArchiveStore(BackupConfig config, Action<string>? log = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _log =
            log
            ?? (
                s =>
                {
                    if (_config.Verbose)
                        Utilities.ConWrite(s);
                }
            );
        try
        {
            Directory.CreateDirectory(_config.DataPath);
        }
        catch (Exception ex)
        {
            Utilities.Error(_config.DataPath, nameof(Directory.CreateDirectory), ex);
            throw;
        }
    }

    public string DataPath => _config.DataPath;

    public IReadOnlyDictionary<string, string> Arlist => _arlist;

    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Preflist =>
        _preflist.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyCollection<string>)kvp.Value.ToList().AsReadOnly()
        );

    public IReadOnlyDictionary<string, long> Stats => _stats;
    public long PackSum { get; private set; }

    public void BuildIndex()
    {
        var root = _config.DataPath;
        if (!Directory.Exists(root))
            return;

        foreach (var entry in Directory.GetFileSystemEntries(root).OrderBy(e => e))
            MkarlistInternal(entry);
    }

    public string? GetTargetPathForHash(string hash)
    {
        if (_config.Verbose)
            _log?.Invoke($"GetTargetPathForHash: {hash}");

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
        var prefixList = Regex
            .Split(prefix, "(..)")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
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

        var list = _preflist.ContainsKey(prefix) ? _preflist[prefix].ToList() : new List<string>();
        var nlist = list.Count;

        if (nlist > _config.PrefixSplitThreshold)
        {
            lock (_reorgLock)
            {
                list = _preflist.ContainsKey(prefix)
                    ? _preflist[prefix].ToList()
                    : new List<string>();
                nlist = list.Count;
                if (nlist > _config.PrefixSplitThreshold)
                {
                    if (_config.Verbose)
                        _log?.Invoke($"*** reorganizing '{prefix}' [{nlist} entries]");

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
                            _preflist.TryAdd(JoinPrefix(prefix, dir), new HashSet<string>());
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
                            Utilities.Error($"{from} -> {to}", nameof(File.Move), ex);
                            continue;
                        }

                        var newpfx = JoinPrefix(prefix, dir);
                        _arlist[f] = newpfx;
                        var set = _preflist.GetOrAdd(newpfx, p => new HashSet<string>());
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
        var pset = _preflist.GetOrAdd(prefix, _ => new HashSet<string>());
        lock (pset)
        {
            pset.Add(hash);
        }

        return Path.Combine(_config.DataPath, prefix, hash);
    }

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
                Utilities.Error(outFile, nameof(BZip2OutputStream), ex);
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
                _log?.Invoke(hash);
        }
        else
        {
            _stats.AddOrUpdate("duplicate_blocks", 1, (_, v) => v + 1);
            var dataLen2 = data.Length;
            _stats.AddOrUpdate("duplicate_bytes", dataLen2, (_, v) => v + dataLen2);
            if (_config.Verbose)
                _log?.Invoke($"{hash} already exists");
        }

        return hash;
    }

    public List<string> SaveStream(
        Stream fileStream,
        long size,
        string tag,
        Action<long>? progress = null
    )
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

    private void MkarlistInternal(string entry)
    {
        if (entry == _config.DataPath)
        {
            if (_config.Verbose)
                _log?.Invoke($"+ {entry}");
            try
            {
                foreach (var e in Directory.GetFileSystemEntries(entry).OrderBy(x => x))
                    MkarlistInternal(e);
            }
            catch (Exception ex)
            {
                Utilities.Error(entry, nameof(Directory.GetFileSystemEntries), ex);
            }

            return;
        }

        var rel = Path.GetRelativePath(_config.DataPath, entry)
            .Replace(Path.DirectorySeparatorChar, '/');
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
                _log?.Invoke($"+ {entry}:{prefix}:{file}");
            var set = _preflist.GetOrAdd(prefix, _ => new HashSet<string>());
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
                Utilities.Error(entry, nameof(Directory.GetFileSystemEntries), ex);
            }
        }
        else if (Regex.IsMatch(file, "^[0-9a-f]+$"))
        {
            _arlist[file] = prefix;
            var set = _preflist.GetOrAdd(prefix, _ => new HashSet<string>());
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

    private static string JoinPrefix(string prefix, string segment)
    {
        return string.IsNullOrEmpty(prefix) ? segment : $"{prefix}/{segment}";
    }

    private void CreateDirectoryWithLogging(string path)
    {
        Directory.CreateDirectory(path);
        if (_config.Verbose)
        {
            const string blue = "\u001b[34m";
            const string reset = "\u001b[0m";
            _log?.Invoke($"{blue}Created directory: {path}{reset}");
        }
    }
}

using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchiveStore;
using OsCallsCommon;
using UtilitiesLibrary;

namespace DeDuBa;

// ReSharper disable once ClassNeverInstantiated.Global
/// <summary>
///     Main backup logic (C# port of the original Perl implementation).
///     Traverses files/directories, serializes metadata using custom pack format, and stores
///     content-addressed chunks in DATA/ with SHA-512 hashing and BZip2 compression.
/// </summary>
public class DedubaClass
{
    private const long Chunksize = 1024 * 1024 * 1024;

    private static string? _startTimestamp;
    private static string? _archive;

    private static string _dataPath = "";
    private static BackupConfig? _config;
    private static IArchiveStore? _archiveStore;
    private static IHighLevelOsApi? _osApi;

    // private static string? _tmpp;

    // private static readonly Dictionary<string, string> Settings = new();
    private static long _ds;
    private static readonly Dictionary<string, List<object>> Dirtmp = [];
    private static readonly Dictionary<string, long> Bstats = [];
    private static readonly Dictionary<Int128, int> Devices = [];
    private static readonly Dictionary<string, string?> Fs2Ino = [];
    private static long _packsum;

    // ############################################################################
    // Live status counters for console output
    private static long _statusFilesDone;
    private static long _statusDirsDone;
    private static long _statusQueueTotal;
    private static long _statusBytesDone;

    // ############################################################################
    // Temporary on-disk hashes for backup data management
    // ############################################################################

    // private static Finfo ToFinfo<T>(T? fi) where T : FileSystemInfo
    // {
    //     var fo = new Finfo();
    //     if (fi is null) return fo;
    //     fo.Exists = fi.Exists;
    //     fo.Extension = fi.Extension;
    //     fo.FullName = fi.FullName;
    //     fo.LinkTarget = fi.LinkTarget ?? "";
    //     fo.Name = fi.Name;
    //     switch (fi)
    //     {
    //         case FileInfo fif:
    //             fo.DirectoryName = fif.DirectoryName ?? "";
    //             break;
    //         case DirectoryInfo /* fid */:
    //             break;
    //         default:
    //             throw new ArgumentException(fi.GetType().AssemblyQualifiedName, nameof(fi));
    //     }

    //     return fo;
    // }

    // # 0 dev      device number of filesystem
    // # 1 ino      inode number
    // # 2 mode     file mode  (type and permissions)
    // # 3 nlink    number of (hard) links to the file
    // # 4 uid      numeric user ID of file's owner
    // # 5 gid      numeric group ID of file's owner
    // # 6 rdev     the device identifier (special files only)
    // # 7 size     total size of file, in bytes
    // # 8 atime    last access time in seconds since the epoch
    // # 9 mtime    last modify time in seconds since the epoch
    // # 10 ctime    inode change time in seconds since the epoch (*)
    // # 11 blksize  preferred I/O size in bytes for interacting with the file (may vary from file to file)
    // # 12 blocks   actual number of system-specific blocks allocated on disk (often, but not always, 512 bytes each)

    /// <summary>
    ///     Entry point for running a backup on the provided list of paths.
    /// </summary>
    /// <param name="argv">Input paths to back up. Paths are canonicalized and validated.</param>
    public static void Backup(string[] argv)
    {
        _startTimestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        Utilities.ConWrite($"DeDuBa Version: {Utilities.GetVersion()}");

        var envArchiveRoot = Environment.GetEnvironmentVariable("DEDU_ARCHIVE_ROOT");
        if (!string.IsNullOrEmpty(envArchiveRoot))
            _archive = envArchiveRoot;
        else
            _archive = Utilities.Testing
                ? Path.Combine(Path.GetTempPath(), "ARCHIVE5")
                : "/archive/backup";
        Utilities.ConWrite(
            $"Archive path: {_archive} (mode: {(Utilities.Testing ? "testing" : "production")})"
        );
        _dataPath = Path.Combine(_archive, "DATA");
        // _tmpp = Path.Combine(_archive, $"tmp.{Process.GetCurrentProcess().Id}");
        try
        {
            _ = Directory.CreateDirectory(_dataPath);
        }
        catch (Exception ex)
        {
            Utilities.Error(_dataPath, nameof(Directory.CreateDirectory), ex);
            throw;
        }

        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                _ = new DirectoryInfo(_dataPath)
                {
                    UnixFileMode = (UnixFileMode)Convert.ToInt32("0711", 8),
                };
        }
        catch (Exception ex)
        {
            Utilities.Error(_dataPath, nameof(DirectoryInfo.UnixFileMode), ex);
            throw;
        }

        var logname = Path.Combine(_archive, "log_" + _startTimestamp);
        try
        {
            try
            {
                Utilities.Log = new StreamWriter(logname);
            }
            catch (Exception ex)
            {
                Utilities.Error(logname, nameof(StreamWriter), ex);
                throw;
            } // STDOUT->autoflush(1);

            // STDERR->autoflush(1);
            Utilities.Log.AutoFlush = true;

            // Ensure per-run static state is reset so multiple tests or calls in the
            // same process do not leak inode/directory state into subsequent runs.
            Dirtmp.Clear();
            Fs2Ino.Clear();
            Devices.Clear();
            Bstats.Clear();
            _statusFilesDone = 0;
            _statusDirsDone = 0;
            _statusQueueTotal = 0;
            _statusBytesDone = 0;
            _packsum = 0;
            _ds = 0;

            //#############################################################################
            // Main program
            //#############################################################################
            Console.Write("\n\nMain program\n");

            try
            {
                // @ARGV = map { canonpath realpath $_ } @ARGV;
                // Initialize high-level OS API early (needed for LStat and path canonicalization calls)
                _osApi = HighLevelOsApiFactory.GetOsApi();

                try
                {
                    Utilities.ConWrite($"Before: {Utilities.Dumper(Utilities.D(argv))}");
                    if (Utilities.VerboseOutput)
                        Utilities.ConWrite(
                            $"Before: {Utilities.Dumper(Utilities.D(argv.Select(_osApi!.Canonicalizefilename)))}"
                        );
                    argv =
                    [
                        .. argv.Select(_osApi!.Canonicalizefilename)
                            .Select(node => node["path"]?.ToString())
                            .Select(path => path != null ? Path.GetFullPath(path) : ""),
                    ];

                    // Safety: refuse to backup the archive itself or any path inside the archive/data store.
                    if (!string.IsNullOrEmpty(_archive))
                        foreach (var root in argv)
                        {
                            if (string.IsNullOrEmpty(root))
                                continue;
                            if (IsPathWithinArchive(root))
                            {
                                var msg =
                                    $"Refusing to back up '{root}' because it is within the archive path '{_archive}'.";
                                Utilities.Error(
                                    root,
                                    nameof(Backup),
                                    new InvalidOperationException(msg)
                                );
                                throw new InvalidOperationException(msg);
                            }
                        }
                }
                catch (Exception ex)
                {
                    Utilities.Error(nameof(argv), nameof(IHighLevelOsApi.Canonicalizefilename), ex);
                    throw;
                }

                if (Utilities.VerboseOutput)
                {
                    Utilities.ConWrite("Filtered:");
                    Utilities.ConWrite($"{Utilities.Dumper(Utilities.D(argv))}");
                }

                foreach (var root in argv)
                {
                    InodeData? minimalData;
                    try
                    {
                        minimalData = _osApi!.CreateMinimalInodeDataFromPath(root);
                    }
                    catch (Exception ex)
                    {
                        Utilities.Error(
                            root,
                            nameof(IHighLevelOsApi.CreateMinimalInodeDataFromPath),
                            ex
                        );
                        throw;
                    }

                    // Extract device ID from Device field
                    var deviceId = (minimalData?.Device ?? 0);

                    Devices.TryAdd(deviceId, 0);
                    Devices[deviceId]++;
                }

                Utilities.ConWrite(Utilities.Dumper(Utilities.D(Devices)));

                // ############################################################################

                // tie % arlist,   'DB_File', undef;    #, "$tmpp.arlist";
                // tie % preflist, 'DB_File', undef;    #, "$tmpp.preflist";

                Utilities.ConWrite("Getting archive state\n");
                // Initialize config & archive store
                _config = BackupConfig.FromUtilities();
                _config = new BackupConfig(
                    _archive ?? _config.ArchiveRoot,
                    _config.ChunkSize,
                    Utilities.Testing,
                    Utilities.VerboseOutput,
                    _config.PrefixSplitThreshold
                );
                _dataPath = _config.DataPath;
                _archiveStore = new ArchiveStore.ArchiveStore(
                    _config,
                    msg => Utilities.ConWrite(msg)
                );
                _archiveStore.BuildIndex();

                if (Utilities.VerboseOutput)
                {
                    Utilities.ConWrite("Before backup:\n");
                    foreach (var kvp in _archiveStore!.Arlist)
                        Utilities.ConWrite(
                            Utilities.Dumper(
                                new KeyValuePair<string, object?>($"Arlist['{kvp.Key}']", kvp.Value)
                            )
                        );

                    // Iterate over preflist
                    // Iterate over preflist
                    foreach (var kvp in _archiveStore!.Preflist)
                        Utilities.ConWrite(
                            Utilities.Dumper(
                                new KeyValuePair<string, object?>(
                                    $"Preflist['{kvp.Key}']",
                                    kvp.Value
                                )
                            )
                        );
                }

                // ##############################################################################

                Utilities.ConWrite("Backup starting\n");

                Backup_worker(argv);

                Utilities.ConWrite("\n");

                // ##############################################################################

                // # my $status = unxz $input => $output [,OPTS] or print "\n", __LINE__, ' ', scalar localtime, ' ',  "\n", __ "unxz failed: $UnXzError\n" if TESTING;

                if (Utilities.VerboseOutput)
                    Utilities.ConWrite(Utilities.Dumper(Utilities.D(Dirtmp)));

                if (Utilities.VerboseOutput)
                    Utilities.ConWrite(Utilities.Dumper(Utilities.D(Devices)));

                Utilities.ConWrite(Utilities.Dumper(Utilities.D(_archiveStore.Stats ?? Bstats)));

                // untie %arlist;
                // untie %preflist;
                // unlink "$tmpp.arlist", "$tmpp.preflist";

                Utilities.Log.Close();

                Utilities.ConWrite("Backup done\n");
            }
            catch (Exception ex)
            {
                Utilities.Error(logname, nameof(Utilities.Log.Close), ex);
                // Rethrow validation errors (e.g. we refused to backup the archive root) so callers/tests can
                // observe the exception. Other exceptions continue to be handled here.
                if (ex is InvalidOperationException)
                    throw;
            }
        }
        finally
        {
            Utilities.Log?.Close();
        }
    }

    // private struct Finfo
    // {
    //     public bool Exists;
    //     public string DirectoryName;
    //     public string Extension;
    //     public string FullName;
    //     public string LinkTarget;
    //     public string Name;
    // }

    // ############################################################################
    // Subroutines
    // ############################################################################

    // ############################################################################
    // build arlist/preflist

    // ############################################################################
    // Helper method to create directories with optional blue console output in test mode

    /// <summary>
    ///     Creates a directory and optionally logs the creation in blue text if verbose output is enabled.
    /// </summary>
    /// <param name="path">Absolute path to the directory to create.</param>
    /// <remarks>
    ///     Uses <see cref="M:System.IO.Directory.CreateDirectory(System.String)" /> which is idempotent (succeeds if directory
    ///     already exists).
    /// </remarks>
    private static void CreateDirectoryWithLogging(string path)
    {
        Directory.CreateDirectory(path);
        if (Utilities.VerboseOutput)
        {
            const string blue = "\u001b[34m";
            const string reset = "\u001b[0m";
            Console.WriteLine($"{blue}Created directory: {path}{reset}");
        }
    }

    /// <summary>
    ///     Determines if a given path is equal to or a descendant of the current archive root.
    ///     Returns false if the archive is not configured or the path cannot be resolved.
    /// </summary>
    private static bool IsPathWithinArchive(string path)
    {
        if (string.IsNullOrEmpty(_archive) || string.IsNullOrEmpty(path))
            return false;
        try
        {
            var pathFull = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            var archiveFull = Path.GetFullPath(_archive).TrimEnd(Path.DirectorySeparatorChar);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (pathFull.Equals(archiveFull, comparison))
                return true;
            var prefix = archiveFull + Path.DirectorySeparatorChar;
            return pathFull.StartsWith(prefix, comparison);
        }
        catch
        {
            return false; // If something goes wrong normal validation will handle it later
        }
    }

    /// <summary>
    ///     Joins a storage prefix (e.g., "aa/bb") with a child segment (e.g., "cc") without introducing a leading slash.
    ///     Ensures the result is never rooted, so Path.Combine(_dataPath, prefix, ...) stays under _dataPath.
    /// </summary>
    private static string JoinPrefix(string prefix, string segment)
    {
        return string.IsNullOrEmpty(prefix) ? segment : $"{prefix}/{segment}";
    }

    // ############################################################################
    // Structured data
    //
    // unpacked: [ ... [ ... ] ... 'string' \number ... ]
    //
    // packed: JSON serialization format

    /// <summary>
    ///     Serializes a value using JSON format for storage in the archive.
    /// </summary>
    private static string Sdpack(object? v, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length > 0 && Utilities.VerboseOutput)
            Utilities.ConWrite(
                $"{name}: {Utilities.Dumper(Utilities.D(v?.GetType().FullName), Utilities.D(v))}"
            );
        if (v is InodeData inode)
            return JsonSerializer.Serialize(inode, typeof(InodeData));
        return JsonSerializer.Serialize(v);
    }

    // ReSharper disable once UnusedMember.Global
    /// <summary>
    ///     Parses a serialized value produced by <see cref="Sdpack(object?, string)" />.
    /// </summary>
    private static object? Sdunpack(string value)
    {
        return JsonSerializer.Deserialize<object>(value);
    }

    /// <summary>
    ///     Reads a file/stream in fixed-size chunks, stores them in the archive, and returns their hashes.
    /// </summary>
    private static List<string> Save_file(Stream fileStream, long size, string tag)
    {
        var pathForStatus = (tag ?? "").Split(' ')[0];
        return _archiveStore!.SaveStream(
            fileStream,
            size,
            tag ?? "",
            bytes =>
            {
                _statusBytesDone += bytes;
                var processed = Math.Max(0, (int)(size - bytes));
                var percent = size > 0 ? processed * 100.0 / size : 100.0;
                var queuedRemaining = Math.Max(
                    0,
                    _statusQueueTotal - (_statusFilesDone + _statusDirsDone)
                );
                Utilities.Status(
                    _statusFilesDone,
                    _statusDirsDone,
                    queuedRemaining,
                    _statusBytesDone,
                    pathForStatus,
                    percent
                );
            }
        );
    }

    // ##############################################################################
    /// <summary>
    ///     Processes a set of filesystem entries, iterating through directories and emitting inode records.
    ///     Uses a queue-based work queue to avoid recursion.
    /// </summary>
    private static void Backup_worker(string[] filesToBackup)
    {
        // Suppress verbose debug output while running the worker; errors still print (colorized)
        // var prevVerboseOutput = Utilities.VerboseOutput;
        Utilities.VerboseOutput = false;
        try
        {
            // Initialize work queue with initial files (FIFO queue for breadth-first traversal)
            var workQueue = new Queue<string>();

            // Enqueue initial files in order
            foreach (var entry in filesToBackup.OrderBy(e => e, StringComparer.Ordinal))
                workQueue.Enqueue(entry);

            _statusQueueTotal += filesToBackup.Length;

            // Process work queue until empty
            while (workQueue.Count > 0)
            {
                var entry = workQueue.Dequeue();

                // Skip any entries that live inside the archive/data store so we do not recurse into it
                if (!string.IsNullOrEmpty(_archive) && IsPathWithinArchive(entry))
                {
                    if (Utilities.VerboseOutput)
                        Utilities.ConWrite(
                            $"Skipping archive/internal path during traversal: {entry}"
                        );
                    continue;
                }

                var volume = Path.GetPathRoot(entry);
                var directories = Path.GetDirectoryName(entry);
                var file = Path.GetFileName(entry);
                var dir = Path.Combine(volume ?? string.Empty, directories ?? string.Empty);
                var name = file;
                InodeData? minimalData = null;

                // $dir is the current directory name,
                // $name is the current filename within that directory
                // $entry is the complete pathname to the file.
                var start = DateTime.Now;
                try
                {
                    minimalData =
                        _osApi!.CreateMinimalInodeDataFromPath(entry)
                        ?? throw new Win32Exception("null inodeData");
                }
                catch (Exception ex)
                {
                    Utilities.Error(
                        entry,
                        nameof(IHighLevelOsApi.CreateMinimalInodeDataFromPath),
                        ex
                    );
                }

                var stDev = (minimalData?.Device ?? 0);
                var stIno = (minimalData?.FileIndex ?? 0);
                if (
                    Devices.ContainsKey(stDev)
                    && _dataPath != null
                    && Path.GetRelativePath(_dataPath, entry).StartsWith("..")
                )
                {
                    // 0 dev      device number of filesystem
                    // 1 ino      inode number
                    // 2 mode     file mode  (type and permissions)
                    // 3 nlink    number of (hard) links to the file
                    // 4 uid      numeric user ID of file's owner
                    // 5 gid      numeric group ID of file's owner
                    // 6 rdev     the device identifier (special files only)
                    // 7 size     total size of file, in bytes
                    // 8 atime    last access time in seconds since the epoch
                    // 9 mtime    last modify time in seconds since the epoch
                    // # 10 ctime    inode change time in seconds since the epoch (*)
                    // # 11 blksize  preferred I/O size in bytes for interacting with the file (may vary from file to file)
                    // # 12 blocks   actual number of system-specific blocks allocated on disk (often, but not always, 512 bytes each)
                    var fsfid = Sdpack(new List<object?> { stDev, stIno }, "fsfid");
                    // Debug: record whether we've already seen this fsfid in this run
                    try
                    {
                        Utilities.Log?.Write(
                            $"[DBG-FSFID] fsfid={fsfid} present={Fs2Ino.ContainsKey(fsfid)}\n"
                        );
                    }
                    catch { }

                    var old = Fs2Ino.ContainsKey(fsfid);
                    var flags = minimalData?.Flags ?? new HashSet<string>();
                    string report;
                    var fileSize = minimalData?.Size ?? 0;
                    if (old)
                    {
                        report = $"[OLD: {fileSize:d} -> duplicate]";
                    }
                    else
                    {
                        Fs2Ino[fsfid] = Sdpack(null, "");
                        if (flags.Contains("dir"))
                            try
                            {
                                // Enqueue children into work queue and update total
                                var childEntries = _osApi
                                    ?.ListDirectory(entry)
                                    .Where(x => !IsPathWithinArchive(x))
                                    .ToList();

                                _statusQueueTotal += childEntries?.Count ?? 0;

                                if (childEntries != null)
                                    foreach (var childEntry in childEntries)
                                        workQueue.Enqueue(childEntry);
                            }
                            catch (OsException ex)
                            {
                                Utilities.Error(entry, "ListDirectory", ex);
                            }
                            catch (Exception ex)
                            {
                                Utilities.Error(entry, nameof(Directory.GetFileSystemEntries), ex);
                            }

                        _packsum = 0;
                        _ds = 0;

                        // Use IHighLevelOsApi to collect all metadata
                        InodeData inodeData;
                        try
                        {
                            if (minimalData is null)
                            {
                                Utilities.Error(
                                    entry,
                                    nameof(IHighLevelOsApi.CreateMinimalInodeDataFromPath),
                                    new InvalidOperationException("null inodeData")
                                );
                                continue;
                            }

                            inodeData = minimalData;
                            inodeData = _osApi!.CompleteInodeDataFromPath(
                                entry,
                                ref inodeData,
                                _archiveStore!
                            );
                            flags = inodeData.Flags;
                            fileSize = inodeData.Size;
                        }
                        catch (OsException ex)
                        {
                            Utilities.Error(entry, "CompleteInodeDataFromPath", ex);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            Utilities.Error(entry, "CompleteInodeDataFromPath", ex);
                            continue;
                        }

                        // For directories, we need to handle Dirtmp content separately
                        // because it's built up as children are processed
                        if (flags.Contains("dir"))
                        {
                            var dataIsdir = Sdpack(
                                Dirtmp.TryGetValue(entry, out var value) ? value : [],
                                "dir"
                            );
                            Dirtmp.Remove(entry);
                            var size = dataIsdir.Length;
                            MemoryStream dirMem;
                            try
                            {
                                var dataBytes = Encoding.UTF8.GetBytes(dataIsdir);
                                dirMem = new MemoryStream(dataBytes);
                            }
                            catch (Exception ex)
                            {
                                Utilities.Error(entry, nameof(MemoryStream), ex);
                                continue;
                            }

                            // Replace the empty hashes with directory content hashes
                            var dirHashes = Save_file(dirMem, size, $"{entry} $data $dirtmp");
                            inodeData.Hashes = [.. dirHashes];
                            _ds = dataIsdir.Length;
                        }
                        else if (flags.Contains("lnk"))
                        {
                            // Track symlink target size for reporting
                            var hashArray = inodeData.Hashes.ToArray();
                            _ds = hashArray.Length > 0 ? (int)inodeData.Size : 0;
                        }

                        // Serialize InodeData and save to archive
                        var data = Sdpack(inodeData, "inode");
                        MemoryStream mem;
                        try
                        {
                            var dataBytes = Encoding.UTF8.GetBytes(data);
                            mem = new MemoryStream(dataBytes);
                        }
                        catch (Exception ex)
                        {
                            Utilities.Error(entry, nameof(MemoryStream), ex);
                            continue;
                        }

                        // open my $mem, '<:unix mmap raw scalar', \$data or die "\$data: $!";
                        var hashes = Save_file(mem, data.Length, $"{entry} $data @inode");
                        var ino = Sdpack(hashes, "fileid");
                        Fs2Ino[fsfid] = ino;
                        TimeSpan? needed = DateTime.Now.Subtract(start);
                        var speed =
                            needed.Value.TotalSeconds > 0
                                ? (double?)_ds / needed.Value.TotalSeconds
                                : null;
                        report = $"[{inodeData.Size:d} -> {_packsum:d}: {needed:c}s]";
                    }

                    if (!Dirtmp.ContainsKey(dir))
                        Dirtmp[dir] = [];
                    if (Fs2Ino.TryGetValue(fsfid, out var fs2InoValue))
                        Dirtmp[dir].Add(new object?[] { name, fs2InoValue });

                    // Compute a canonical form for the entry to make logs unambiguous
                    var canonicalForLog = entry;
                    try
                    {
                        canonicalForLog = Path.GetFullPath(entry);
                    }
                    catch
                    {
                        canonicalForLog = entry;
                    }

                    Utilities.Log?.Write(
                        $"{BitConverter.ToString(Encoding.UTF8.GetBytes(Fs2Ino[fsfid] ?? string.Empty)).Replace("-", "")} {entry} canonical={canonicalForLog} {report}\n"
                    );
                    // Extra debug/logging: include canonicalized path, relative path from `dir`, filename, and a short flags indicator
                    try
                    {
                        var canonical = string.Empty;
                        try
                        {
                            canonical = Path.GetFullPath(entry);
                        }
                        catch
                        {
                            canonical = entry;
                        }

                        var relativeToDir = string.Empty;
                        try
                        {
                            relativeToDir = Path.GetRelativePath(dir, entry);
                        }
                        catch
                        {
                            relativeToDir = entry;
                        }

                        var baseName = Path.GetFileName(entry);
                        var flagShort = string.Join(',', flags);
                        Utilities.Log?.Write(
                            $"[DBG] entry={entry} canonical={canonical} rel={relativeToDir} name={baseName} flags={flagShort}\n"
                        );
                    }
                    catch (Exception ex)
                    {
                        // Do not let debug logging break backup; write error to utilities
                        Utilities.Error(entry, "debug-log", ex);
                    }

                    // File or directory completed -> update counters and status line
                    var isDir = flags.Contains("dir");
                    if (isDir)
                        _statusDirsDone++;
                    else
                        _statusFilesDone++;
                    var queuedRemaining = Math.Max(
                        0,
                        _statusQueueTotal - (_statusFilesDone + _statusDirsDone)
                    );
                    // Percent for completed item is 100; for directory we don't compute size percent
                    var sizeFinal = fileSize;
                    var percentDone = sizeFinal > 0 && !flags.Contains("dir") ? 100.0 : double.NaN;
                    Utilities.Status(
                        _statusFilesDone,
                        _statusDirsDone,
                        queuedRemaining,
                        _statusBytesDone,
                        entry,
                        percentDone
                    );
                }
                else
                {
                    Utilities.Error(entry, "pruning");
                }
            }
        }
        finally
        {
            // Ensure VerboseOutput flag is restored after this worker scope
            // Utilities.VerboseOutput = prevVerboseOutput;
            // Move to next line after status updates
            if (Utilities.VerboseOutput)
                Console.WriteLine();
        }
    }
}

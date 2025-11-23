using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.BZip2;
using UtilitiesLibrary;
#if WINDOWS
using OsCallsWindows;
#else
using OsCallsLinux;
#endif

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

    // private static string? _tmpp;

    // private static readonly Dictionary<string, string> Settings = new();
    private static long _ds;
    private static readonly Dictionary<string, List<object>> Dirtmp = [];
    private static readonly Dictionary<string, long> Bstats = [];
    private static readonly Dictionary<long, int> Devices = [];
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

        _archive = Utilities.Testing ? "/home/kai/projects/Backup/ARCHIVE4" : "/archive/backup";
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

            //#############################################################################
            // Main program
            //#############################################################################
            Console.Write("\n\nMain program\n");

            try
            {
                // @ARGV = map { canonpath realpath $_ } @ARGV;
                try
                {
                    Utilities.ConWrite($"Before: {Utilities.Dumper(Utilities.D(argv))}");
                    if (Utilities.VerboseOutput)
                        Utilities.ConWrite(
                            $"Before: {Utilities.Dumper(Utilities.D(argv.Select(FileSystem.Canonicalizefilename)))}"
                        );
                    argv =
                    [
                        .. argv.Select(FileSystem.Canonicalizefilename)
                            .Select(node => node["path"]?.ToString())
                            .Select(path => path != null ? Path.GetFullPath(path) : ""),
                    ];
                }
                catch (Exception ex)
                {
                    Utilities.Error(nameof(argv), nameof(FileSystem.Canonicalizefilename), ex);
                    throw;
                }

                if (Utilities.VerboseOutput)
                {
                    Utilities.ConWrite("Filtered:");
                    Utilities.ConWrite($"{Utilities.Dumper(Utilities.D(argv))}");
                }

                foreach (var root in argv)
                {
                    JsonNode? st;
                    try
                    {
                        st = FileSystem.LStat(root);
                    }
                    catch (Exception ex)
                    {
                        Utilities.Error(root, nameof(FileSystem.LStat), ex);
                        throw;
                    }

                    var i = st["st_dev"]?.GetValue<long>() ?? 0;
                    Devices.TryAdd(i, 0);
                    Devices[i]++;
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
                _archiveStore = new ArchiveStore(_config, msg => Utilities.ConWrite(msg));
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

                Utilities.ConWrite(Utilities.Dumper(Utilities.D(_archiveStore?.Stats ?? Bstats)));

                // untie %arlist;
                // untie %preflist;
                // unlink "$tmpp.arlist", "$tmpp.preflist";

                Utilities.Log.Close();

                Utilities.ConWrite("Backup done\n");
            }
            catch (Exception ex)
            {
                Utilities.Error(logname, nameof(Utilities.Log.Close), ex);
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
    ///     Uses <see cref="Directory.CreateDirectory" /> which is idempotent (succeeds if directory already exists).
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
    ///     Joins a storage prefix (e.g., "aa/bb") with a child segment (e.g., "cc") without introducing a leading slash.
    ///     Ensures the result is never rooted, so Path.Combine(_dataPath, prefix, ...) stays under _dataPath.
    /// </summary>
    private static string JoinPrefix(string prefix, string segment)
    {
        return string.IsNullOrEmpty(prefix) ? segment : $"{prefix}/{segment}";
    }

    // ############################################################################
    // find place for hashed file, or note we already have it

    /// <summary>
    //     Structured data
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

                var volume = Path.GetPathRoot(entry);
                var directories = Path.GetDirectoryName(entry);
                var file = Path.GetFileName(entry);
                var dir = Path.Combine(volume ?? string.Empty, directories ?? string.Empty);
                var name = file;
                JsonNode? statBuf = null;

                // $dir is the current directory name,
                // $name is the current filename within that directory
                // $entry is the complete pathname to the file.
                var start = DateTime.Now;
                try
                {
                    statBuf = FileSystem.LStat(entry);
                    if (statBuf == null)
                        throw new Win32Exception("null statBuf");
                    // var sb = statBuf.Value;
                    // Utilities.ConWrite(
                    //     $"{sb.StDev} {sb.StIno} {sb.StIsDir} {sb.StIsLnk} {sb.StIsReg} {sb.StUid} {sb.StGid} {sb.StMode} {sb.StNlink} {sb.StRdev} {sb.StSize} {sb.StBlocks} {sb.StBlksize} {sb.StAtim} {sb.StCtim} {sb.StMtim} {sb.GetHashCode()}");
                }
                catch (Exception ex)
                {
                    Utilities.Error(entry, nameof(FileSystem.LStat), ex);
                }

                var stDev = statBuf?["st_dev"]?.GetValue<long>() ?? 0;
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
                    var fsfid = Sdpack(
                        new List<object?> { stDev, statBuf?["st_ino"]?.GetValue<long>() ?? 0 },
                        "fsfid"
                    );
                    var old = Fs2Ino.ContainsKey(fsfid);
                    // Always compute the file-type flags from statBuf so subsequent code
                    // can consult them without directly accessing statBuf S_IS* fields.
                    var flags = new HashSet<string>();
                    if (statBuf is JsonObject statObj)
                        foreach (var kvp in statObj)
                        {
                            var key = kvp.Key;
                            // Match S_IS* or S_TYPEIS* boolean fields
                            if (key.StartsWith("S_IS") || key.StartsWith("S_TYPEIS"))
                                if (kvp.Value?.GetValue<bool>() ?? false)
                                {
                                    // Transform flag name: remove prefix and convert to lowercase
                                    var flagName = key.StartsWith("S_TYPEIS")
                                        ? key[8..].ToLowerInvariant()
                                        : key[4..].ToLowerInvariant();
                                    flags.Add(flagName);
                                }
                        }

                    string report;
                    var fileSize = statBuf?["st_size"]?.GetValue<long>() ?? 0;
                    if (old)
                    {
                        report = $"[{fileSize:d} -> duplicate]";
                    }
                    else
                    {
                        Fs2Ino[fsfid] = Sdpack(null, "");
                        if (flags.Contains("dir"))
                            try
                            {
                                var entries = Directory.GetFileSystemEntries(entry); // Assuming no . ..
                                // Enqueue children into work queue and update total
                                var childEntries = entries
                                    .Where(x => !x.StartsWith(".."))
                                    .Select(x => Path.Combine(entry, x))
                                    .OrderBy(x => x, StringComparer.Ordinal)
                                    .ToList();

                                _statusQueueTotal += childEntries.Count;

                                foreach (var childEntry in childEntries)
                                    workQueue.Enqueue(childEntry);
                            }
                            catch (Exception ex)
                            {
                                Utilities.Error(entry, nameof(Directory.GetFileSystemEntries), ex);
                            }

                        _packsum = 0;
                        // lstat(entry);

                        var groupId = statBuf?["st_gid"]?.GetValue<long>() ?? 0;
                        var userId = statBuf?["st_uid"]?.GetValue<long>() ?? 0;
                        var inodeData = new InodeData
                        {
                            FileId = Sdunpack(fsfid) as JsonElement?,
                            Mode = statBuf?["st_mode"]?.GetValue<long>() ?? 0,
                            Flags = flags,
                            NLink = statBuf?["st_nlink"]?.GetValue<long>() ?? 0,
                            Uid = Convert.ToInt64(userId),
#if !WINDOWS
                            UserName =
                                UserGroupDatabase
                                    .GetPwUid(Convert.ToInt64(userId))["pw_name"]
                                    ?.ToString()
                                ?? Convert.ToInt64(userId).ToString(),
#else
                            UserName = Convert.ToInt64(userId).ToString(),
#endif
                            Gid = Convert.ToInt64(groupId),
#if !WINDOWS
                            GroupName =
                                UserGroupDatabase
                                    .GetGrGid(Convert.ToInt64(groupId))["gr_name"]
                                    ?.ToString()
                                ?? Convert.ToInt64(groupId).ToString(),
#else
                            GroupName = Convert.ToInt64(groupId).ToString(),
#endif
                            RDev = statBuf?["st_rdev"]?.GetValue<long>() ?? 0,
                            Size = fileSize,
                            MTime = statBuf?["st_mtim"]?.GetValue<double>() ?? 0,
                            CTime = statBuf?["st_ctim"]?.GetValue<double>() ?? 0,
                        };

                        // Read ACLs and xattrs before file type specialization
                        string[] aclHashes = [];
                        Dictionary<string, IEnumerable<string>> xattrHashes = [];

                        // Read ACL data
#if !WINDOWS
                        try
                        {
                            var aclAccessResult = Acl.GetFileAccess(entry);
                            if (
                                aclAccessResult is JsonObject aclAccessObj
                                && aclAccessObj.ContainsKey("acl_text")
                            )
                            {
                                var aclText = aclAccessObj["acl_text"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(aclText))
                                {
                                    var aclBytes = Encoding.UTF8.GetBytes(aclText);
                                    var aclMem = new MemoryStream(aclBytes);
                                    aclHashes =
                                    [
                                        .. Save_file(aclMem, aclBytes.Length, $"{entry} $acl"),
                                    ];
                                }
                            }

                            // For directories, also read default ACL
                            if (flags.Contains("dir"))
                            {
                                var aclDefaultResult = Acl.GetFileDefault(entry);
                                if (
                                    aclDefaultResult is JsonObject aclDefaultObj
                                    && aclDefaultObj.ContainsKey("acl_text")
                                )
                                {
                                    var aclDefaultText =
                                        aclDefaultObj["acl_text"]?.ToString() ?? "";
                                    if (!string.IsNullOrEmpty(aclDefaultText))
                                    {
                                        var aclDefaultBytes = Encoding.UTF8.GetBytes(
                                            aclDefaultText
                                        );
                                        var aclDefaultMem = new MemoryStream(aclDefaultBytes);
                                        var defaultHashes = Save_file(
                                            aclDefaultMem,
                                            aclDefaultBytes.Length,
                                            $"{entry} $acl_default"
                                        );
                                        // Combine both access and default ACL hashes
                                        aclHashes = [.. aclHashes, .. defaultHashes];
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // ACL reading may fail for files without ACLs or unsupported filesystems
                            // This is not fatal, just log and continue
                            if (Utilities.Testing)
                                Utilities.ConWrite($"ACL read failed for {entry}: {ex.Message}");
                        }
#endif

                        // Read extended attributes
#if !WINDOWS
                        try
                        {
                            var xattrListResult = Xattr.ListXattr(entry);
                            if (xattrListResult is JsonArray xattrArray)
                                foreach (var xattrNameNode in xattrArray)
                                {
                                    var xattrName = xattrNameNode?.ToString();
                                    if (string.IsNullOrEmpty(xattrName))
                                        continue;

                                    try
                                    {
                                        var xattrValueResult = Xattr.GetXattr(entry, xattrName);
                                        if (
                                            xattrValueResult is JsonObject xattrValueObj
                                            && xattrValueObj.ContainsKey("value")
                                        )
                                        {
                                            var xattrValue =
                                                xattrValueObj["value"]?.ToString() ?? "";
                                            var xattrBytes = Encoding.UTF8.GetBytes(xattrValue);
                                            var xattrMem = new MemoryStream(xattrBytes);
                                            var xattrHashList = Save_file(
                                                xattrMem,
                                                xattrBytes.Length,
                                                $"{entry} $xattr:{xattrName}"
                                            );
                                            xattrHashes[xattrName] = xattrHashList;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // Individual xattr reading may fail
                                        if (Utilities.Testing)
                                            Utilities.ConWrite(
                                                $"Xattr read failed for {entry}:{xattrName}: {ex.Message}"
                                            );
                                    }
                                }
                        }
                        catch (Exception ex)
                        {
                            // Xattr reading may fail for files without xattrs or unsupported filesystems
                            // This is not fatal, just log and continue
                            if (Utilities.Testing)
                                Utilities.ConWrite($"Xattr list failed for {entry}: {ex.Message}");
                        }
#endif

                        inodeData.Acl = aclHashes;
                        inodeData.Xattr = xattrHashes;

                        string[] hashes = [];
                        _ds = 0;
                        MemoryStream mem;
                        if (flags.Contains("reg"))
                        {
                            var size = fileSize;
                            if (size != 0)
                                try
                                {
                                    var fileStream = File.OpenRead(entry);
                                    hashes = [.. Save_file(fileStream, size, entry)];
                                }
                                catch (Exception ex)
                                {
                                    Utilities.Error(entry, nameof(File.OpenRead), ex);
                                    continue;
                                }
                        }
                        else if (flags.Contains("lnk"))
                        {
                            string? dataIslink;
                            try
                            {
                                // ReadLink returns a JsonObject with a "path" field set to the symlink target.
                                var linkNode = FileSystem.ReadLink(entry);
                                dataIslink = linkNode?["path"]?.GetValue<string>() ?? string.Empty;
                            }
                            catch (Exception ex)
                            {
                                Utilities.Error(entry, nameof(FileSystem.ReadLink), ex);
                                continue;
                            }

                            var size = dataIslink.Length;
                            MemoryStream? mem1 = null;
                            try
                            {
                                var dataBytes = Encoding.UTF8.GetBytes(dataIslink);
                                mem1 = new MemoryStream(dataBytes);
                            }
                            catch (Exception ex)
                            {
                                Utilities.Error(entry, nameof(MemoryStream), ex);
                            }

                            // open my $mem, '<:unix mmap raw', \$data or die "\$data: $!";
                            hashes = [.. Save_file(mem1!, size, $"{entry} $data readlink")];
                            _ds = dataIslink.Length;
                        }
                        else if (flags.Contains("dir"))
                        {
                            var dataIsdir = Sdpack(
                                Dirtmp.TryGetValue(entry, out var value) ? value : [],
                                "dir"
                            );
                            Dirtmp.Remove(entry);
                            var size = dataIsdir.Length;
                            try
                            {
                                var dataBytes = Encoding.UTF8.GetBytes(dataIsdir);
                                mem = new MemoryStream(dataBytes);
                            }
                            catch (Exception ex)
                            {
                                Utilities.Error(entry, nameof(MemoryStream), ex);
                                continue;
                            }

                            // open my $mem, '<:unix mmap raw', \$data or die "\$data: $!";
                            hashes = [.. Save_file(mem, size, $"{entry} $data $dirtmp")];
                            _ds = dataIsdir.Length;
                        }

                        inodeData.Hashes = hashes;
                        var data = Sdpack(inodeData, "inode");

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
                        hashes = [.. Save_file(mem, data.Length, $"{entry} $data @inode")];
                        var ino = Sdpack(hashes, "fileid");
                        Fs2Ino[fsfid] = ino;
                        TimeSpan? needed = DateTime.Now.Subtract(start);
                        var speed =
                            needed.Value.TotalSeconds > 0
                                ? (double?)_ds / needed.Value.TotalSeconds
                                : null;
                        report = $"[{fileSize:d} -> {_packsum:d}: {needed:c}s]";
                    }

                    if (!Dirtmp.ContainsKey(dir))
                        Dirtmp[dir] = [];
                    if (Fs2Ino.TryGetValue(fsfid, out var fs2InoValue))
                        Dirtmp[dir].Add(new object?[] { name, fs2InoValue });
                    Utilities.Log?.Write(
                        $"{BitConverter.ToString(Encoding.UTF8.GetBytes(Fs2Ino[fsfid] ?? string.Empty)).Replace("-", "")} {entry} {report}\n"
                    );
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

    // Represents inode metadata collected for a filesystem entry prior to packing.
    private sealed class InodeData
    {
        [JsonPropertyName("fi")]
        public required JsonElement? FileId { get; init; }

        [JsonPropertyName("md")]
        public long Mode { get; init; }

        [JsonPropertyName("fl")]
        public required HashSet<string> Flags { get; init; }

        [JsonPropertyName("nl")]
        public long NLink { get; init; }

        [JsonPropertyName("ui")]
        public long Uid { get; init; }

        [JsonPropertyName("un")]
        public string UserName { get; init; } = string.Empty;

        [JsonPropertyName("gi")]
        public long Gid { get; init; }

        [JsonPropertyName("gn")]
        public string GroupName { get; init; } = string.Empty;

        [JsonPropertyName("rd")]
        public long RDev { get; init; }

        [JsonPropertyName("sz")]
        public long Size { get; init; }

        [JsonPropertyName("mt")]
        public double MTime { get; init; }

        [JsonPropertyName("ct")]
        public double CTime { get; init; }

        [JsonPropertyName("hs")]
        public IEnumerable<string> Hashes { get; set; } = [];

        [JsonPropertyName("ac")]
        public IEnumerable<string> Acl { get; set; } = [];

        [JsonPropertyName("xa")]
        public Dictionary<string, IEnumerable<string>> Xattr { get; set; } = [];

        /// <summary>
        ///     Returns a compact string representation for diagnostics.
        /// </summary>
        public override string ToString()
        {
            var hashCount = Hashes.Count();
            var hashInfo = hashCount > 0 ? $"{hashCount} hash(es)" : "no hashes";
            var aclCount = Acl.Count();
            var aclInfo = aclCount > 0 ? $"{aclCount} acl hash(es)" : "";
            var xattrCount = Xattr.Count;
            var xattrInfo = xattrCount > 0 ? $"{xattrCount} xattr(s)" : "";
            var extras = string.Join(
                " ",
                new[] { aclInfo, xattrInfo }.Where(s => !string.IsNullOrEmpty(s))
            );
            return $"[mode=0{Mode:o} nlink={NLink} {UserName}({Uid}):{GroupName}({Gid}) rdev={RDev} size={Size} mtime={MTime} ctime={CTime} {hashInfo} {extras}]";
        }
    }
}

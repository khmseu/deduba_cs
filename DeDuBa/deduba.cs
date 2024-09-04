using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

internal partial class Program
{
    private const long CHUNKSIZE = 1024 * 1024 * 1024;
    private static readonly string START_TIMESTAMP = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

    private const bool TESTING = true;
    private static readonly string archive = TESTING ? "/home/kai/projects/Backup/ARCHIVE2" : "/archive/backup";
    private static readonly string dataPath = Path.Combine(archive, "DATA");
    private static readonly string tmpp = Path.Combine(archive, $"tmp.{Process.GetCurrentProcess().Id}");

    private static long ds;
    private static readonly Dictionary<string, List<object>> dirtmp = [];
    private static readonly Dictionary<string, long> bstats = [];
    private static readonly Dictionary<string, int> devices = [];
    private static readonly Dictionary<string, string?> fs2ino = [];
    private static long packsum;


    // ############################################################################
    // Temporary on-disk hashes for backup data management
    // ############################################################################
    // arlist: hash -> part of filename between $data_path and actual file
    private static readonly Dictionary<string, string> arlist = [];

    // preflist: list of files and directories under a given prefix
    // (as \0-separated list)
    private static readonly Dictionary<string, string> preflist = [];

    private static StreamWriter LOG;

    private static void Main(string[] args)
    {
        string archive = TESTING ? "/home/kai/projects/Backup/ARCHIVE2" : "/archive/backup";
        string dataPath = Path.Combine(archive, "DATA");
        string tmpp = Path.Combine(archive, $"tmp.{Process.GetCurrentProcess().Id}");

        _ = Directory.CreateDirectory(dataPath, (UnixFileMode)0711);


        var logname = Path.Combine(archive, "log_" + START_TIMESTAMP);
        LOG = new StreamWriter(logname);


        try
        {
            args = args.Select(Path.GetFullPath).ToArray();

            foreach (var root in args)
            {
                var st = new FileInfo(root);
                if (st.Exists) devices[st.Attributes.ToString()] = 1;
            }

            LogLine($"Devices: {string.Join(", ", devices.Keys)}");

            BackupWorkerMethod(args);

            LogLine("Backup done");
        }
        finally
        {
            LOG.Close();
        }
    }

    // ############################################################################
    // Subroutines
    // ############################################################################

    // ############################################################################
    // errors
    private static void Error(string file, string op, [CallerLineNumber] int lineNumber = 0)
    {
        var msg = $"*** {file}: {op}: {new Win32Exception().Message}\n";
        if (TESTING) ConWrite(msg, lineNumber);
        if (LOG != null) LOG.Write(msg);
        else
            throw new Exception(msg);
    }

    private static void ConWrite(string msg, int explicitLineNumber = 0, [CallerLineNumber] int lineNumber = 0)
    {
        Console.Write($"\n{(explicitLineNumber != 0 ? explicitLineNumber : (lineNumber))} {DateTime.Now.ToString()} {msg}");
    }

    // ############################################################################
    // build arlist/preflist

    private static void mkarlist(params string[] entries)
    {
        foreach (var entry in entries.OrderBy(e => e))
        {
            if (entry == dataPath)
            {
                if (TESTING) ConWrite($"+ {entry}");
                try
                {
                    var dirEntries = Directory.GetFileSystemEntries(entry);// Assuming no . ..
                    if (TESTING) Console.Write($"\t{dirEntries.Length} entries");
                    mkarlist(dirEntries);
                    if (TESTING) Console.WriteLine($"\tdone {entry}");
                }
                catch (Exception ex)
                {
                    Error(entry, "GetFileSystemEntries");
                }

                continue;
            }

            var match = Regex.Match(entry, $"^{Regex.Escape(dataPath)}/?(.*)/([^/]+)$");
            var prefix = match.Groups[1].Value;
            var file = match.Groups[2].Value;
            if (Regex.IsMatch(file, "^[0-9a-f][0-9a-f]$"))
            {
                if (TESTING) ConWrite($"+ {entry}:{prefix}:{file}");
                preflist.TryAdd(prefix, "");
                preflist[prefix] += $"{file}/\0";
                try
                {
                    var dirEntries = Directory.GetFileSystemEntries(entry);// Assuming no . ..
                    if (TESTING) Console.Write($"\t{dirEntries.Length} entries");
                    mkarlist(dirEntries);
                    if (TESTING) Console.WriteLine($"\tdone {entry}");
                }
                catch (Exception ex)
                {
                    Error(entry, ex.Message);
                }
            }
            else if (Regex.IsMatch(file, "^[0-9a-f]+$"))
            {
                arlist[file] = prefix;
                preflist.TryAdd(prefix, "");
                preflist[prefix] += $"{file}\0";
            }
            else
            {
                ConWrite($"Bad entry in archive: {entry}");
            }

        }
    }


    // ############################################################################
    // find place for hashed file, or note we already have it

    private static string? hash2fn(string hash)
    {
        if (TESTING) ConWrite($"{hash}");

        if (arlist.TryGetValue(hash, out var value))
        {
            packsum += new FileInfo(Path.Combine(dataPath, value, hash)).Length;
            return null;
        }

        var prefix = hash;
        var prefixList = Regex.Split(prefix, "(..)").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        prefixList.RemoveAt(prefixList.Count - 1);

        while (prefixList.Count > 0)
        {
            prefix = string.Join("/", prefixList);
            if (preflist.ContainsKey(prefix)) break;
            prefixList.RemoveAt(prefixList.Count - 1);
        }

        prefix = string.Join("/", prefixList);
        var list = preflist[prefix].Split('\0').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var nlist = list.Count;

        if (nlist > 255)
        {
            if (TESTING) ConWrite($"*** reorganizing '{prefix}' [{nlist} entries]\n");

            var depth = prefixList.Count;
            var plen = 2 * depth;
            var newDirs = new HashSet<string>();

            foreach (var f in list)
                if (f.EndsWith("/"))
                    newDirs.Add(f);

            for (var n = 0; n <= 0xff; n++)
            {
                var dir = $"{n:x2}/";
                if (!newDirs.Contains(dir))
                {
                    Directory.CreateDirectory(Path.Combine(dataPath, prefix, dir));
                    newDirs.Add(dir);
                    preflist[$"{prefix}/{dir}"] = "";
                }
            }

            foreach (var f in list)
                if (!f.EndsWith("/"))
                {
                    var dir = f.Substring(plen, 2);
                    var de = $"{dir}/";
                    if (!newDirs.Contains(de))
                    {
                        Directory.CreateDirectory(Path.Combine(dataPath, prefix, dir));
                        newDirs.Add(de);
                    }

                    var from = Path.Combine(dataPath, prefix, f);
                    var to = Path.Combine(dataPath, prefix, dir, f);
                    try
                    {
                        File.Move(from, to);
                    }
                    catch (Exception ex)
                    {
                        Error($"{from} -> {to}", ex.Message);
                        continue;
                    }

                    var newpfx = $"{prefix}/{dir}";
                    arlist[f] = newpfx;
                    preflist.TryAdd(newpfx, "");
                    preflist[newpfx] += $"{f}\0";
                }

            preflist[prefix] = string.Join("\0", newDirs) + "\0";
            var dir2 = hash.Substring(plen, 2);
            prefix = $"{prefix}/{dir2}";
        }
        else
        {
            if (TESTING) ConWrite($"+++ not too large: '{prefix}' entries = {list.Count}\n");
        }

        arlist[hash] = prefix;
        preflist.TryAdd(prefix, "");
        preflist[prefix] += $"{hash}\0";
        return Path.Combine(dataPath, prefix, hash);
    }


    public static string SdPack(object value, string name)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        var type = value.GetType();
        ConWrite($"{name}: {type}, {value}");

        switch (type.Name)
        {
            case "String":
                return value != null ? "s" + value : "u";
            case "Int32":
                var intValue = (int)value;
                return intValue >= 0
                    ? "n" + BitConverter.GetBytes((ushort)intValue)
                    : "N" + BitConverter.GetBytes((ushort)-intValue);
            case "Array":
                var array = (Array)value;
                var packedArray = new List<string>();
                foreach (var item in array) packedArray.Add(SdPack(item, ""));
                return "l" + string.Join("", packedArray);
            default:
                throw new InvalidOperationException("unexpected type " + type.Name);
        }
    }

    public static object? SdUnpack(string value)
    {
        var p = value.Substring(0, 1);
        var d = value.Substring(1);
        switch (p)
        {
            case "u":
                return null;
            case "s":
                return d;
            case "n":
                return BitConverter.ToUInt16(Encoding.UTF8.GetBytes(d), 0);
            case "N":
                return (ushort)-BitConverter.ToUInt16(Encoding.UTF8.GetBytes(d), 0);
            case "l":
                var unpackedList = new List<object>();
                // Assuming unpacked data is separated by a delimiter, adjust as necessary
                foreach (var item in d.Split(','))
                    if (item != null)
                        unpackedList.Add(SdUnpack(item) ?? String.Empty);
                return unpackedList.ToArray();
            default:
                throw new InvalidOperationException("unexpected type " + p);
        }
    }

    public static object[] Usr(int uid)
    {
        return [uid, GetUserName(uid)];
    }

    public static object[] Grp(int gid)
    {
        return [gid, GetGroupName(gid)];
    }

    private static string SaveData(string data)
    {
        using var sha512 = SHA512.Create();
        var hashBytes = sha512.ComputeHash(Encoding.UTF8.GetBytes(data));
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        var outFile = HashToFileName(hash);

        if (outFile != null)
        {
            bstats["saved_blocks"]++;
            bstats["saved_bytes"] += data.Length;

            try
            {
                File.WriteAllText(outFile, data); // Simulating bzip2 compression
            }
            catch (Exception e)
            {
                Error($"Error: {outFile}, bzip2 failed: {e.Message}", "bzip2");
                packsum += new FileInfo(outFile).Length;
                return hash;
            }

            packsum += new FileInfo(outFile).Length;
            if (TESTING) ConWrite($"{hash}");
        }
        else
        {
            bstats["duplicate_blocks"]++;
            bstats["duplicate_bytes"] += data.Length;
            if (TESTING) ConWrite($"{hash} already exists");
        }

        return hash;
    }

    private static List<string> SaveFile(Stream fileStream, long size, string tag)
    {
        var hashes = new List<string>();

        while (size > 0)
        {
            var data = new byte[CHUNKSIZE];
            var bytesRead = fileStream.Read(data, 0, (int)Math.Min(CHUNKSIZE, size));
            if (bytesRead == 0) break;

            var dataString = Encoding.UTF8.GetString(data, 0, bytesRead);
            hashes.Add(SaveData(dataString));
            size -= bytesRead;
            ds += bytesRead;
        }

        if (TESTING) ConWrite($"eof: {string.Join(", ", hashes)}");
        return hashes;
    }

    private static string GetUserName(int uid)
    {
        // Implement logic to get username by uid
        return "username"; // Placeholder
    }

    private static string GetGroupName(int gid)
    {
        // Implement logic to get group name by gid
        return "groupname"; // Placeholder
    }

    private static string HashToFileName(string hash)
    {
        // Implement logic to convert hash to filename
        return hash + ".txt"; // Placeholder
    }

    private static void BackupWorkerMethod(string[] entries)
    {
        foreach (var entry in entries.OrderBy(e => e))
        {
            var volume = Path.GetPathRoot(entry);
            var directories = Path.GetDirectoryName(entry);
            var file = Path.GetFileName(entry);
            var dir = Path.Combine(volume ?? string.Empty, directories ?? string.Empty);
            var name = file;

            LogLine($"Handling file: {dir}, {name}, {entry}");

            try
            {
                var stat = new FileInfo(entry);
                if (!stat.Exists)
                {
                    LogError(entry, "File does not exist");
                    continue;
                }

                if (devices.ContainsKey(stat.Attributes.ToString()) &&
                    Path.GetRelativePath(dataPath, entry).StartsWith(".."))
                {
                    var fsfid = $"{stat.Attributes.ToString()}:{stat.LastWriteTimeUtc.Ticks}";
                    var old = fs2ino.ContainsKey(fsfid);
                    string report;

                    if (!old)
                    {
                        fs2ino[fsfid] = null;

                        if (stat.Attributes.HasFlag(FileAttributes.Directory))
                            foreach (var subEntry in Directory.GetFiles(entry))
                                BackupWorkerMethod([subEntry]);

                        packsum = 0;
                        var inode = new List<object>
                        {
                            stat.Attributes.ToString(),
                            stat.LastWriteTimeUtc,
                            stat.Length
                        };

                        var hashes = new List<string>();
                        ds = 0;

                        if (stat.Attributes.HasFlag(FileAttributes.ReparsePoint)) // FIXME
                        {
                            var size = stat.Length;
                            if (size > 0)
                            {
                                using var fileStream = new FileStream(entry, FileMode.Open, FileAccess.Read);
                                hashes = SaveFile(fileStream, size, entry);
                            }
                        }
                        else if (stat.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            var data = File.ReadAllText(entry);
                            var size = data.Length;
                            LogLine($"Data: {data}");

                            using var memStream = new MemoryStream(Encoding.UTF8.GetBytes(data));
                            hashes = SaveFile(memStream, size, $"{entry} readlink");
                            ds = size;
                        }
                        else if (stat.Attributes.HasFlag(FileAttributes.Directory))
                        {
                            var data = string.Join(",",
                                dirtmp.TryGetValue(entry, out var value) ? value : new List<object>());
                            var size = data.Length;
                            LogLine($"Data: {data}");

                            using var memStream = new MemoryStream(Encoding.UTF8.GetBytes(data));
                            hashes = SaveFile(memStream, size, $"{entry} dirtmp");
                            ds = size;
                        }

                        inode.Add(hashes);
                        var dataPack = string.Join(",", inode);
                        LogLine($"Data Pack: {dataPack}");

                        using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(dataPack)))
                        {
                            hashes = SaveFile(memStream, dataPack.Length, $"{entry} inode");
                        }

                        var ino = string.Join(",", hashes);
                        fs2ino[fsfid] = ino;
                        var needed = DateTime.UtcNow.Ticks - DateTime.UtcNow.Ticks; // Placeholder for timing
                        var speed = needed > 0 ? ds / needed : (long?)null;

                        report = $"[{stat.Length} -> {packsum}: {needed}]";
                    }
                    else
                    {
                        report = $"[{stat.Length} -> duplicate]";
                    }

                    if (!dirtmp.ContainsKey(dir)) dirtmp[dir] = new List<object>();
                    dirtmp[dir].Add(new { name, V = fs2ino[fsfid] });

                    LogLine($"{fs2ino[fsfid]} {entry} {report}");
                }
                else
                {
                    LogError(entry, "Pruning");
                }
            }
            catch (Exception ex)
            {
                LogError(entry, ex.Message);
            }
        }
    }


    private static void LogLine(string message)
    {
        if (TESTING)
        {
            ConWrite($"{message}");
            LOG.WriteLine($"{DateTime.Now}: {message}");
        }
    }

    private static void LogError(string entry, string message)
    {
        Error($"Error: {entry} - {message}", "?");
        LOG.WriteLine($"Error: {entry} - {message}");
    }



}
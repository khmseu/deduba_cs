using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bzip2;

namespace DeDuBa;

public class DedubaClass
{
    private const long Chunksize = 1024 * 1024 * 1024;

    public static bool Testing = true;
    private static string? _startTimestamp;
    private static string? _archive;
    private static string _dataPath = "";
    private static string? _tmpp;

    private static readonly Dictionary<string, string> Settings = new();
    private static long _ds;
    private static readonly Dictionary<string, List<object>> Dirtmp = [];
    private static readonly Dictionary<string, long> Bstats = [];
    private static readonly Dictionary<ulong, int> Devices = [];
    private static readonly Dictionary<string, string?> Fs2Ino = [];
    private static long _packsum;

    private static StreamWriter? _log;

    // ############################################################################
    // Temporary on-disk hashes for backup data management
    // ############################################################################
    // arlist: hash -> part of filename between $data_path and actual file
    private static readonly Dictionary<string, string> Arlist = [];

    // preflist: list of files and directories under a given prefix
    // (as \0-separated list)
    private static readonly Dictionary<string, string> Preflist = [];
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private static byte[] _buf = new byte[1];


    public static void Backup(string[] argv)
    {
        _startTimestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

        _archive = Testing ? "/home/kai/projects/Backup/ARCHIVE2" : "/archive/backup";
        _dataPath = Path.Combine(_archive, "DATA");
        _tmpp = Path.Combine(_archive, $"tmp.{Process.GetCurrentProcess().Id}");

        _ = Directory.CreateDirectory(_dataPath);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var dirInfo = new DirectoryInfo(_dataPath);
            dirInfo.UnixFileMode = (UnixFileMode)0711;
        }

        var logname = Path.Combine(_archive, "log_" + _startTimestamp);
        _log = new StreamWriter(logname);
        // STDOUT->autoflush(1);
        // STDERR->autoflush(1);
        _log.AutoFlush = true;


        //#############################################################################
        // Main program
        //#############################################################################

        try
        {
            // @ARGV = map { canonpath realpath $_ } @ARGV;
            argv = argv.Select(Path.GetFullPath).ToArray();

            foreach (var root in argv)
            {
                var st = Lstat(root);
                if (st != null && st.Length > 0) Devices[(ulong)st[0]] = 1;
            }

            ConWrite(Dumper(D(Devices)));

            // ############################################################################

            // tie % arlist,   'DB_File', undef;    #, "$tmpp.arlist";
            // tie % preflist, 'DB_File', undef;    #, "$tmpp.preflist";
            Preflist[""] = "";

            ConWrite("Getting archive state\n");

            Mkarlist(_dataPath);

            if (Testing)
            {
                ConWrite("Before backup:\n");
                foreach (var kvp in Arlist)
                    ConWrite(Dumper(new KeyValuePair<string, object?>($"{nameof(Arlist)}[{kvp.Key}]", kvp.Value)));

                // Iterate over preflist
                foreach (var kvp in Preflist)
                    ConWrite(Dumper(new KeyValuePair<string, object?>($"{nameof(Preflist)}[{kvp.Key}]", kvp.Value)));
            }

            // ##############################################################################

            ConWrite("Backup starting\n");

            backup_worker(argv);

            // ##############################################################################

            // # my $status = unxz $input => $output [,OPTS] or print "\n", __LINE__, ' ', scalar localtime, ' ',  "\n", __ "unxz failed: $UnXzError\n" if TESTING;

            if (Testing) ConWrite(Dumper(D(Dirtmp)));

            if (Testing) ConWrite(Dumper(D(Devices)));

            ConWrite(Dumper(D(Bstats)));

            // untie %arlist;
            // untie %preflist;
            // unlink "$tmpp.arlist", "$tmpp.preflist";

            _log.Close();

            ConWrite("Backup done\n");
        }
        catch (Exception ex)
        {
            Error(logname, nameof(_log.Close), ex);
        }
        finally
        {
            _log.Close();
        }
    }

    private static KeyValuePair<string, object?> D(object? value,
        [CallerArgumentExpression(nameof(value))]
        string name = "")
    {
        return new KeyValuePair<string, object?>(name, value);
    }

    private static string Dumper(params KeyValuePair<string, object?>[] values)
    {
        string[] ret = [];
        foreach (var kvp in values)
        {
            var jsonOutput = JsonSerializer.Serialize(kvp.Value, SerializerOptions);
            ret = ret.Append($"{kvp.Key} = {jsonOutput}\n")
                .ToArray();
        }

        return string.Join("", ret);
    }

    // ############################################################################
    // Subroutines
    // ############################################################################

    // ############################################################################
    // errors

    private static void Error(string file, string op, [CallerLineNumber] int lineNumber = 0)
    {
        Error(file, op, new Win32Exception(), lineNumber);
    }

    private static void Error(string file, string op, Exception ex, [CallerLineNumber] int lineNumber = 0)
    {
        var msg = $"*** {file}: {op}: {ex.Message}\n";
        if (Testing) ConWrite(msg, lineNumber);
        if (_log != null) _log.Write(msg);
        else
            throw new Exception(msg);
    }

    private static void Warn(string msg, [CallerLineNumber] int lineNumber = 0)
    {
        ConWrite($"WARN: {msg}\n", lineNumber);
    }

    private static void ConWrite(string msg, [CallerLineNumber] int lineNumber = 0)
    {
        Console.Write(
            $"\n{lineNumber} {DateTime.Now} {msg}");
    }

    // ############################################################################
    // build arlist/preflist

    private static void Mkarlist(params string[] filePaths)
    {
        foreach (var entry in filePaths.OrderBy(e => e))
        {
            if (entry == _dataPath)
            {
                if (Testing) ConWrite($"+ {entry}");
                try
                {
                    var entries = Directory.GetFileSystemEntries(entry); // Assuming no . ..
                    if (Testing) Console.Write($"\t{entries.Length} entries");
                    Mkarlist(entries);
                    if (Testing) Console.WriteLine($"\tdone {entry}");
                }
                catch (Exception ex)
                {
                    Error(entry, nameof(Directory.GetFileSystemEntries), ex);
                }

                continue;
            }

            var match = Regex.Match(entry, $"^{Regex.Escape(_dataPath)}/?(.*)/([^/]+)$");
            var prefix = match.Groups[1].Value;
            var file = match.Groups[2].Value;
            if (Regex.IsMatch(file, "^[0-9a-f][0-9a-f]$"))
            {
                if (Testing) ConWrite($"+ {entry}:{prefix}:{file}");
                Preflist.TryAdd(prefix, "");
                Preflist[prefix] += $"{file}/\0";
                try
                {
                    var dirEntries = Directory.GetFileSystemEntries(entry); // Assuming no . ..
                    if (Testing) Console.Write($"\t{dirEntries.Length} entries");
                    Mkarlist(dirEntries);
                    if (Testing) Console.WriteLine($"\tdone {entry}");
                }
                catch (Exception ex)
                {
                    Error(entry, nameof(Directory.GetFileSystemEntries), ex);
                }
            }
            else if (Regex.IsMatch(file, "^[0-9a-f]+$"))
            {
                Arlist[file] = prefix;
                Preflist.TryAdd(prefix, "");
                Preflist[prefix] += $"{file}\0";
            }
            else
            {
                Warn($"Bad entry in archive: {entry}");
            }
        }
    }

    // ############################################################################
    // find place for hashed file, or note we already have it

    private static string? Hash2Fn(string hash)
    {
        if (Testing) ConWrite(Dumper(D(hash)));

        if (Arlist.TryGetValue(hash, out var value))
        {
            _packsum += new FileInfo(Path.Combine(_dataPath, value, hash)).Length;
            return null;
        }

        var prefix = hash;
        var prefixList = Regex.Split(prefix, "(..)").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        prefixList.RemoveAt(prefixList.Count - 1);

        while (prefixList.Count > 0)
        {
            prefix = string.Join("/", prefixList);
            if (Preflist.ContainsKey(prefix)) break;
            prefixList.RemoveAt(prefixList.Count - 1);
        }

        if (prefixList.Count == 0) prefix = string.Join("/", prefixList);
        var list = Preflist[prefix].Split('\0').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var nlist = list.Count;

        if (nlist > 255)
        {
            // dir becoming too large, move files into subdirs
            ConWrite($"*** reorganizing '{prefix}' [{nlist} entries]\n");
            if (Testing) ConWrite($"{Dumper(D(list))}\n");

            var depth = prefixList.Count;
            var plen = 2 * depth;
            var newDirs = new HashSet<string>();

            foreach (var f in list)
                if (f.EndsWith("/"))
                    newDirs.Add(f);

            for (var n = 0x00; n <= 0xff; n++)
            {
                var dir = $"{n:x2}";
                var de = $"{dir}/";
                if (!newDirs.Contains(de))
                {
                    Directory.CreateDirectory(Path.Combine(_dataPath, prefix, dir));
                    newDirs.Add(de);
                    Preflist[$"{prefix}/{dir}"] = "";
                }
            }

            foreach (var f in list)
                if (!f.EndsWith("/"))
                {
                    var dir = f.Substring(plen, 2);
                    var de = $"{dir}/";
                    if (!newDirs.Contains(de))
                    {
                        Directory.CreateDirectory(Path.Combine(_dataPath, prefix, dir));
                        newDirs.Add(de);
                    }

                    var from = Path.Combine(_dataPath, prefix, f);
                    var to = Path.Combine(_dataPath, prefix, dir, f);
                    try
                    {
                        File.Move(from, to);
                    }
                    catch (Exception ex)
                    {
                        Error($"{from} -> {to}", nameof(File.Move), ex);
                        continue;
                    }

                    var newpfx = $"{prefix}/{dir}";
                    Arlist[f] = newpfx;
                    Preflist.TryAdd(newpfx, "");
                    Preflist[newpfx] += $"{f}\0";
                }

            Preflist[prefix] = string.Join("\0", newDirs) + "\0";
            var dir2 = hash.Substring(plen, 2);
            prefix = $"{prefix}/{dir2}";

            // print "\n", __LINE__, ' ', scalar localtime, ' ',  "After reorg:\n" if TESTING;
            // while (my ($k, $v) = each %arlist) {
            // print "\n", __LINE__, ' ', scalar localtime, ' ',  Data::Dumper->Dump([$v], ["\$arlist{'$k'}"]) if TESTING;
            //           }
            // while (my ($k, $v) = each %preflist) {
            // print "\n", __LINE__, ' ', scalar localtime, ' ',  Data::Dumper->Dump([$v], ["\$preflist{'$k'}"]) if TESTING;
            //           }
            // print "\n", __LINE__, ' ', scalar localtime, ' ',  "\n" if TESTING;
        }
        else
        {
            if (Testing) ConWrite($"+++ not too large: '{prefix}' entries = {list.Count}\n");
        }

        Arlist[hash] = prefix;
        Preflist.TryAdd(prefix, "");
        Preflist[prefix] += $"{hash}\0";
        return Path.Combine(_dataPath, prefix, hash);
    }

    public static string pack_w<NUM>(NUM value) where NUM : INumber<NUM>
    {
        if (NUM.IsNegative(value))
            throw new InvalidOperationException("Cannot compress negative numbers in " + nameof(pack_w));
        var buf = new byte[Marshal.SizeOf(typeof(NUM)) * 8 / 7 + 1];
        var inIndex = buf.Length;
        do
        {
            buf[--inIndex] = (byte)(((dynamic)value & 0x7F) | 0x80);
            value = (NUM)((dynamic)value >> 7);
        } while ((dynamic)value > 0);

        buf[buf.Length - 1] &= 0x7F; /* clear continue bit */
        return new string(buf.Skip(inIndex).Select(b => (char)b).ToArray());
    }

    public static ulong unpack_w(string value, ref int s)
    {
        ulong auv = 0;
        while (s < value.Length)
        {
            byte ch;
            ch = (byte)value[s++];
            auv = (auv << 7) | ((ulong)ch & 0x7f);
            if (ch < 0x80) return auv;
        }

        throw new InvalidOperationException("Unterminated compressed integer in " + nameof(unpack_w));
    }

    public static ulong unpack_w(string value)
    {
        var s = 0;
        var ret = unpack_w(value, ref s);
        if (s < value.Length)
            throw new InvalidOperationException("Junk after compressed integer in " + nameof(unpack_w));
        return ret;
    }

    // ############################################################################
    // Structured data
    // 
    // unpacked: [ ... [ ... ] ... 'string' \number ... ]
    // 
    // packed: w/a strings, w/w unsigned numbers, w/(a) lists
    // 

    public static string Sdpack(object? v, string name)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (v == null) return "u";
        var t = v.GetType();
        if (name.Length > 0 && Testing) ConWrite($"{name}: {Dumper(D(t), D(v))}");

        switch (t.Name)
        {
            case "String":
                return "s" + v;
            case "Int32":
            case "Int64":
                var intValue = (long)v;
                return intValue >= 0
                    ? "n" + pack_w((ulong)intValue)
                    : "N" + pack_w((ulong)-intValue);
            case "Array":
                var array = (Array)v;
                var ary = new List<string>();
                foreach (var item in array) ary.Add(Sdpack(item, ""));
                return "l" + pack_w((ulong)ary.Count) + string.Join("", ary.Select(x => pack_w((ulong)x.Length) + x));
            default:
                throw new InvalidOperationException("unexpected type " + t.Name);
        }
    }

    public static object? Sdunpack(string value)
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
                return unpack_w(d);
            case "N":
                return -(long)unpack_w(d);
            case "l":
                var unpackedList = new List<object?>();
                var s = 0;
                var n = unpack_w(d, ref s);
                while (n-- >= 0)
                {
                    var il = unpack_w(d, ref s);
                    unpackedList.Add(Sdunpack(d.Substring(s, (int)il)));
                }

                if (s < d.Length)
                    throw new InvalidOperationException("Junk after compressed integer in " + nameof(Sdunpack));
                return unpackedList.ToArray();
            default:
                throw new InvalidOperationException("unexpected type " + p);
        }
    }


    private static PasswdEntry GetPasswd(uint uid)
    {
        var pwPtr = LibCalls.getpwuid(uid);
        if (pwPtr == IntPtr.Zero) throw new Exception("Failed to get passwd struct");

        return Marshal.PtrToStructure<PasswdEntry>(pwPtr);
    }


    public static object[] Usr(int uid)
    {
        return [uid, GetPasswd((uint)uid).PwPasswd];
    }

    private static GroupEntry GetGroup(uint gid)
    {
        var grPtr = LibCalls.getgrgid(gid);
        if (grPtr == IntPtr.Zero) throw new Exception("Failed to get group struct");

        return Marshal.PtrToStructure<GroupEntry>(grPtr);
    }

    public static object[] Grp(int gid)
    {
        return [gid, GetGroup((uint)gid).GrName];
    }

    private static string save_data(string data)
    {
        var hashBytes = SHA512.HashData(data.ToArray().Select(x => (byte)x).ToArray());
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        var outFile = Hash2Fn(hash);

        if (outFile != null)
        {
            Bstats["saved_blocks"]++;
            Bstats["saved_bytes"] += data.Length;

            try
            {
                var outputStream = File.Create(outFile);
                var bzip2OutputStream = new BZip2OutputStream(outputStream);
                bzip2OutputStream.Write(data.Select(x => (byte)x).ToArray());
                bzip2OutputStream.Close();
            }
            catch (Exception ex)
            {
                Error(outFile, nameof(BZip2OutputStream), ex);
                _packsum += new FileInfo(outFile).Length;
                return hash; // ???
            }

            _packsum += new FileInfo(outFile).Length;
            if (Testing) ConWrite(hash);
        }
        else
        {
            Bstats["duplicate_blocks"]++;
            Bstats["duplicate_bytes"] += data.Length;
            if (Testing) ConWrite($"{hash} already exists");
        }

        return hash;
    }

    private static List<string> save_file(Stream fileStream, long size, string tag)
    {
        var hashes = new List<string>();

        // my @layers = PerlIO::get_layers($file, details => 1);
        // print "\n", __LINE__, ' ', scalar localtime, ' input: ', Dumper(@layers, $size) if TESTING;
        while (size > 0)
            try
            {
                var data = new byte[Chunksize];
                var n12 = fileStream.Read(data, 0, (int)Math.Min(Chunksize, size));
                if (Testing) ConWrite($"chunk: {Dumper(D(size), D(n12))}");
                if (n12 == 0) break;

                hashes.Add(save_data(new string(data.AsSpan(0, n12).ToArray().Select(x => (char)x).ToArray())));
                size -= n12;
                _ds += n12;
            }
            catch (Exception ex)
            {
                Error(tag, nameof(Stream.Read), ex);
            }

        if (Testing) ConWrite($"eof: {Dumper(D(size), D(hashes))}");
        return hashes;
    }

    private static object[] Lstat(string filename)
    {
        var buf = new LibCalls.StatInfo();
        var ret = LibCalls.lstat(filename, ref buf);
        if (ret != 0) throw new Win32Exception();

        double T2d(LibCalls.TimeSpec t)
        {
            return t.TvSec + (double)t.TvNsec / (1000 * 1000 * 1000);
        }

        return
        [
            buf.StDev,
            buf.StIno,
            buf.StMode,
            buf.StNlink,
            buf.StUid,
            buf.StGid,
            buf.StRdev,
            buf.StSize,
            T2d(buf.StAtim),
            T2d(buf.StMtim),
            T2d(buf.StCtim),
            buf.StBlksize,
            buf.StBlocks
        ];
    }

    private static string Readlink(string path)
    {
        var sz = LibCalls.readlink(path, _buf, (ulong)_buf.Length);
        do
        {
            if (sz == -1) throw new Win32Exception();
            if (sz < _buf.Length) break;
            _buf = new byte[_buf.Length * 2];
        } while (true);

        return new string(_buf.AsSpan(0, (int)sz).ToArray().Select(x => (char)x).ToArray());
    }


    // ##############################################################################
    private static void backup_worker(string[] filesToBackup)
    {
        foreach (var entry in filesToBackup.OrderBy(e => e))
        {
            var volume = Path.GetPathRoot(entry);
            var directories = Path.GetDirectoryName(entry);
            var file = Path.GetFileName(entry);
            if (Testing) ConWrite($"{"=".Repeat(80)}\n");
            if (Testing) ConWrite($"{Dumper(D(entry), D(volume), D(directories), D(file))}");
            var dir = Path.Combine(volume ?? string.Empty, directories ?? string.Empty);
            var name = file;
            object[] statBuf = [];
            DateTime? start = null;
            try
            {
                statBuf = Lstat(entry);

                // $dir is the current directory name,
                // $name is the current filename within that directory
                // $entry is the complete pathname to the file.
                start = DateTime.Now;
                if (Testing) ConWrite($"handle_file: {Dumper(D(dir), D(name), D(entry))}");
            }
            catch (Exception ex)
            {
                Error(entry, nameof(LibCalls.lstat), ex);
            }

            if (Testing) ConWrite(Dumper(D(statBuf[0])));
            if (Devices.ContainsKey((ulong)statBuf[0]) && _dataPath != null &&
                Path.GetRelativePath(_dataPath, entry).StartsWith(".."))
            {
                ConWrite($"stat: {Dumper(D(statBuf))}");

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
                // 10 ctime    inode change time in seconds since the epoch (*)
                // 11 blksize  preferred I/O size in bytes for interacting with the file (may vary from file to file)
                // 12 blocks   actual number of system-specific blocks allocated on disk (often, but not always, 512 bytes each)
                var fsfid = Sdpack((ulong[]) [(ulong)statBuf[0], (ulong)statBuf[1]], "fsfid");
                var old = Fs2Ino.ContainsKey(fsfid);
                string report;
                if (!old)
                {
                    Fs2Ino[fsfid] = Sdpack(null, "");
                    if (LibCalls.S_ISDIR(statBuf))
                        while (true)
                            try
                            {
                                var entries = Directory.GetFileSystemEntries(entry); // Assuming no . ..
                                if (Testing) Console.Write($"\t{entries.Length} entries");
                                backup_worker(entries.Where(x => !x.StartsWith(".."))
                                    .Select(x => Path.Combine(entry, x)).ToArray());
                                if (Testing) Console.WriteLine($"\tdone {entry}");
                            }
                            catch (Exception ex)
                            {
                                Error(entry, nameof(Directory.GetFileSystemEntries), ex);
                            }

                    _packsum = 0;
                    // lstat(entry);
                    var inode = new List<object[]>
                    {
                        new[] { statBuf[2], statBuf[3] },
                        Usr((int)statBuf[4]),
                        Grp((int)statBuf[5]),
                        new[] { statBuf[6], statBuf[7], statBuf[9], statBuf[10] }
                    }.ToArray();
                    string? data;
                    string[] hashes = [];
                    _ds = 0;
                    if (LibCalls.S_ISREG(statBuf))
                    {
                        var size = (long)statBuf[7];
                        if (size != 0)
                            try
                            {
                                var fileStream = File.OpenRead(entry);
                                hashes = save_file(fileStream, size, entry).ToArray();
                            }
                            catch (Exception ex)
                            {
                                Error(entry, nameof(File.OpenRead), ex);
                                continue;
                            }
                    }
                    else if (LibCalls.S_ISLNK(statBuf))
                    {
                        try
                        {
                            data = Readlink(entry);
                        }
                        catch (Exception ex)
                        {
                            Error(entry, nameof(LibCalls.readlink), ex);
                            continue;
                        }

                        var size = data.Length;
                        if (Testing) ConWrite(Dumper(D(data)));
                        MemoryStream? mem1 = null;
                        try
                        {
                            var dataBytes = Encoding.UTF8.GetBytes(data);
                            mem1 = new MemoryStream(dataBytes);
                        }
                        catch (Exception ex)
                        {
                            Error(entry, nameof(MemoryStream), ex);
                        }

                        // open my $mem, '<:unix mmap raw', \$data or die "\$data: $!";
                        hashes = save_file(mem1!, size, $"{entry} $data readlink").ToArray();
                        _ds = data.Length;
                        data = null;
                    }
                    else if (LibCalls.S_ISDIR(statBuf))
                    {
                        var data2 = Sdpack(Dirtmp[entry] ?? [], "dir");
                        Dirtmp.Remove(entry);
                        var size = data2.Length;
                        if (Testing) ConWrite(Dumper(D(data2)));
                        MemoryStream? mem2 = null;
                        try
                        {
                            var dataBytes = Encoding.UTF8.GetBytes(data2);
                            mem2 = new MemoryStream(dataBytes);
                        }
                        catch (Exception ex)
                        {
                            Error(entry, nameof(MemoryStream), ex);
                            continue;
                        }

                        // open my $mem, '<:unix mmap raw', \$data or die "\$data: $!";
                        hashes = save_file(mem2!, size, $"{entry} $data $dirtmp").ToArray();
                        _ds = data2.Length;
                        data2 = null;
                    }

                    if (Testing) ConWrite($"data: {Dumper(D(hashes))}");
                    inode = inode.Append(hashes).ToArray();
                    data = Sdpack(inode, "inode");
                    if (Testing) ConWrite(Dumper(D(data)));
                    MemoryStream? mem = null;
                    try
                    {
                        var dataBytes = Encoding.UTF8.GetBytes(data);
                        mem = new MemoryStream(dataBytes);
                    }
                    catch (Exception ex)
                    {
                        Error(entry, nameof(MemoryStream), ex);
                        continue;
                    }

                    // open my $mem, '<:unix mmap raw scalar', \$data or die "\$data: $!";
                    hashes = save_file(mem!, data.Length, $"{entry} $data @inode").ToArray();
                    var ino = Sdpack(hashes.ToArray(), "fileid");
                    Fs2Ino[fsfid] = ino;
                    TimeSpan? needed = start == null ? null : DateTime.Now.Subtract((DateTime)start);
                    var speed = needed?.TotalSeconds > 0 ? (double?)_ds / needed.Value.TotalSeconds : null;
                    if (Testing) ConWrite($"timing: {Dumper(D(_ds), D(needed), D(speed))}");
                    report = $"[{statBuf[7]:d} -> {_packsum:d}: {needed:d}s]";
                }
                else
                {
                    report = $"[{statBuf[7]:d} -> duplicate]";
                }

                if (!Dirtmp.ContainsKey(dir)) Dirtmp[dir] = new List<object>();
                if (Fs2Ino.TryGetValue(fsfid, out var fs2InoValue))
                    Dirtmp[dir].Add(new object?[] { name, fs2InoValue });
                _log?.Write(
                    $"{BitConverter.ToString(Encoding.UTF8.GetBytes(Fs2Ino[fsfid] ?? string.Empty)).Replace("-", "")} {entry} {report}\n");
                if (Testing) ConWrite($"{"_".Repeat(80)}\n");
            }
            else
            {
                Error(entry, "pruning");
            }

            if (Testing) ConWrite($"{"_".Repeat(80)}\n");
        }
    }

    private static void LogLine(string message)
    {
        if (Testing)
        {
            ConWrite(message);
            _log?.WriteLine($"{DateTime.Now}: {message}");
        }
    }

    private static void LogError(string entry, string message)
    {
        Error($"Error: {entry} - {message}", "?");
        _log?.WriteLine($"Error: {entry} - {message}");
    }


    private readonly struct PasswdEntry
    {
        public readonly string pw_name = "";
        public readonly string PwPasswd = "";
        public readonly uint pw_uid = new();
        public readonly uint pw_gid = new();
        public readonly string pw_gecos = "";
        public readonly string pw_dir = "";
        public readonly string pw_shell = "";

        public PasswdEntry()
        {
        }
    }

    private readonly struct GroupEntry
    {
        public readonly string GrName = "";
        public readonly string gr_passwd = "";
        public readonly uint gr_gid = new();
        public readonly string[] GrMem = new string[0];

        public GroupEntry()
        {
        }
    }
}
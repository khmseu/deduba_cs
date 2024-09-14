using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bzip2;

namespace DeDuBa;

public partial class DedubaClass
{
    private const long CHUNKSIZE = 1024 * 1024 * 1024;

    private const bool TESTING = true;
    private static string? START_TIMESTAMP;
    private static string? archive;
    private static string data_path = "";
    private static string? tmpp;

    private static readonly Dictionary<string, string> settings = new();
    private static long ds;
    private static readonly Dictionary<string, List<object>> dirtmp = [];
    private static readonly Dictionary<string, long> bstats = [];
    private static readonly Dictionary<ulong, int> devices = [];
    private static readonly Dictionary<string, string?> fs2ino = [];
    private static long packsum;

    private static StreamWriter? LOG;

    // ############################################################################
    // Temporary on-disk hashes for backup data management
    // ############################################################################
    // arlist: hash -> part of filename between $data_path and actual file
    private static readonly Dictionary<string, string> arlist = [];

    // preflist: list of files and directories under a given prefix
    // (as \0-separated list)
    private static readonly Dictionary<string, string> preflist = [];
    private static readonly JsonSerializerOptions serializerOptions = new() { WriteIndented = true };
    private static byte[] buf = new byte[1];


    private static void Main(string[] ARGV)
    {
        var START_TIMESTAMP = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

        var archive = TESTING ? "/home/kai/projects/Backup/ARCHIVE2" : "/archive/backup";
        data_path = Path.Combine(archive, "DATA");
        tmpp = Path.Combine(archive, $"tmp.{Process.GetCurrentProcess().Id}");

        _ = Directory.CreateDirectory(data_path);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var dirInfo = new DirectoryInfo(data_path);
            dirInfo.UnixFileMode = (UnixFileMode)0711;
        }

        var logname = Path.Combine(archive, "log_" + START_TIMESTAMP);
        LOG = new StreamWriter(logname);
        // STDOUT->autoflush(1);
        // STDERR->autoflush(1);
        LOG.AutoFlush = true;


        //#############################################################################
        // Main program
        //#############################################################################

        try
        {
            // @ARGV = map { canonpath realpath $_ } @ARGV;
            ARGV = ARGV.Select(Path.GetFullPath).ToArray();

            foreach (var root in ARGV)
            {
                var st = lstat(root);
                if (st != null) devices[(ulong)st[0]] = 1;
            }

            ConWrite(Dumper(D(devices)));

            // ############################################################################

            // tie % arlist,   'DB_File', undef;    #, "$tmpp.arlist";
            // tie % preflist, 'DB_File', undef;    #, "$tmpp.preflist";
            preflist[""] = "";

            ConWrite("Getting archive state\n");

            mkarlist(data_path);

            if (TESTING)
            {
                ConWrite("Before backup:\n");
                foreach (var kvp in arlist)
                    ConWrite(Dumper(new KeyValuePair<string, object?>($"{nameof(arlist)}[{kvp.Key}]", kvp.Value)));

                // Iterate over preflist
                foreach (var kvp in preflist)
                    ConWrite(Dumper(new KeyValuePair<string, object?>($"{nameof(preflist)}[{kvp.Key}]", kvp.Value)));
            }

            // ##############################################################################

            ConWrite("Backup starting\n");

            backup_worker(ARGV);

            // ##############################################################################

            // # my $status = unxz $input => $output [,OPTS] or print "\n", __LINE__, ' ', scalar localtime, ' ',  "\n", __ "unxz failed: $UnXzError\n" if TESTING;

            if (TESTING) ConWrite(Dumper(D(dirtmp)));

            if (TESTING) ConWrite(Dumper(D(devices)));

            ConWrite(Dumper(D(bstats)));

            // untie %arlist;
            // untie %preflist;
            // unlink "$tmpp.arlist", "$tmpp.preflist";

            LOG.Close();

            ConWrite("Backup done\n");
        }
        catch (Exception ex)
        {
            error(logname, nameof(LOG.Close), ex);
        }
        finally
        {
            LOG.Close();
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
            var jsonOutput = JsonSerializer.Serialize(kvp.Value, serializerOptions);
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

    private static void error(string file, string op, [CallerLineNumber] int lineNumber = 0)
    {
        error(file, op, new Win32Exception(), lineNumber);
    }

    private static void error(string file, string op, Exception ex, [CallerLineNumber] int lineNumber = 0)
    {
        var msg = $"*** {file}: {op}: {ex.Message}\n";
        if (TESTING) ConWrite(msg, lineNumber);
        if (LOG != null) LOG.Write(msg);
        else
            throw new Exception(msg);
    }

    private static void warn(string msg, [CallerLineNumber] int lineNumber = 0)
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

    private static void mkarlist(params string[] entries_)
    {
        foreach (var entry in entries_.OrderBy(e => e))
        {
            if (entry == data_path)
            {
                if (TESTING) ConWrite($"+ {entry}");
                try
                {
                    var entries = Directory.GetFileSystemEntries(entry); // Assuming no . ..
                    if (TESTING) Console.Write($"\t{entries.Length} entries");
                    mkarlist(entries);
                    if (TESTING) Console.WriteLine($"\tdone {entry}");
                }
                catch (Exception ex)
                {
                    error(entry, nameof(Directory.GetFileSystemEntries), ex);
                }

                continue;
            }

            var match = Regex.Match(entry, $"^{Regex.Escape(data_path)}/?(.*)/([^/]+)$");
            var prefix = match.Groups[1].Value;
            var file = match.Groups[2].Value;
            if (Regex.IsMatch(file, "^[0-9a-f][0-9a-f]$"))
            {
                if (TESTING) ConWrite($"+ {entry}:{prefix}:{file}");
                preflist.TryAdd(prefix, "");
                preflist[prefix] += $"{file}/\0";
                try
                {
                    var dirEntries = Directory.GetFileSystemEntries(entry); // Assuming no . ..
                    if (TESTING) Console.Write($"\t{dirEntries.Length} entries");
                    mkarlist(dirEntries);
                    if (TESTING) Console.WriteLine($"\tdone {entry}");
                }
                catch (Exception ex)
                {
                    error(entry, nameof(Directory.GetFileSystemEntries), ex);
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
                warn($"Bad entry in archive: {entry}");
            }
        }
    }

    // ############################################################################
    // find place for hashed file, or note we already have it

    private static string? hash2fn(string hash)
    {
        if (TESTING) ConWrite(Dumper(D(hash)));

        if (arlist.TryGetValue(hash, out var value))
        {
            packsum += new FileInfo(Path.Combine(data_path, value, hash)).Length;
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

        if (prefixList.Count == 0) prefix = string.Join("/", prefixList);
        var list = preflist[prefix].Split('\0').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var nlist = list.Count;

        if (nlist > 255)
        {
            // dir becoming too large, move files into subdirs
            ConWrite($"*** reorganizing '{prefix}' [{nlist} entries]\n");
            if (TESTING) ConWrite($"{Dumper(D(list))}\n");

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
                    Directory.CreateDirectory(Path.Combine(data_path, prefix, dir));
                    newDirs.Add(de);
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
                        Directory.CreateDirectory(Path.Combine(data_path, prefix, dir));
                        newDirs.Add(de);
                    }

                    var from = Path.Combine(data_path, prefix, f);
                    var to = Path.Combine(data_path, prefix, dir, f);
                    try
                    {
                        File.Move(from, to);
                    }
                    catch (Exception ex)
                    {
                        error($"{from} -> {to}", nameof(File.Move), ex);
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
            if (TESTING) ConWrite($"+++ not too large: '{prefix}' entries = {list.Count}\n");
        }

        arlist[hash] = prefix;
        preflist.TryAdd(prefix, "");
        preflist[prefix] += $"{hash}\0";
        return Path.Combine(data_path, prefix, hash);
    }

    public static string pack_w(ulong value)
    {
        if (value < 0) throw new InvalidOperationException("Cannot compress negative numbers in " + nameof(pack_w));
        var buf = new byte[sizeof(ulong) * 8 / 7 + 1];
        var inIndex = buf.Length;
        do
        {
            buf[--inIndex] = (byte)((value & 0x7F) | 0x80);
            value >>= 7;
        } while (value > 0);

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

    public static string sdpack(object? v, string name)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (v == null) return "u";
        var t = v.GetType();
        if (name.Length > 0 && TESTING) ConWrite($"{name}: {Dumper(D(t), D(v))}");

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
                foreach (var item in array) ary.Add(sdpack(item, ""));
                return "l" + pack_w((ulong)ary.Count) + string.Join("", ary.Select(x => pack_w((ulong)x.Length) + x));
            default:
                throw new InvalidOperationException("unexpected type " + t.Name);
        }
    }

    public static object? sdunpack(string value)
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
                    unpackedList.Add(sdunpack(d.Substring(s, (int)il)));
                }

                if (s < d.Length)
                    throw new InvalidOperationException("Junk after compressed integer in " + nameof(sdunpack));
                return unpackedList.ToArray();
            default:
                throw new InvalidOperationException("unexpected type " + p);
        }
    }

    [LibraryImport("libc.so.6")]
    private static partial IntPtr getpwuid(uint __uid);

    private static passwdEntry GetPasswd(uint uid)
    {
        var pwPtr = getpwuid(uid);
        if (pwPtr == IntPtr.Zero) throw new Exception("Failed to get passwd struct");

        return Marshal.PtrToStructure<passwdEntry>(pwPtr);
    }


    public static object[] usr(int uid)
    {
        return [uid, GetPasswd((uint)uid).pw_passwd];
    }

    [LibraryImport("libc.so.6")]
    private static partial IntPtr getgrgid(uint __gid);

    private static groupEntry GetGroup(uint gid)
    {
        var grPtr = getgrgid(gid);
        if (grPtr == IntPtr.Zero) throw new Exception("Failed to get group struct");

        return Marshal.PtrToStructure<groupEntry>(grPtr);
    }

    public static object[] grp(int gid)
    {
        return [gid, GetGroup((uint)gid).gr_name];
    }

    private static string save_data(string data)
    {
        var hashBytes = SHA512.HashData(data.ToArray().Select(x => (byte)x).ToArray());
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        var outFile = hash2fn(hash);

        if (outFile != null)
        {
            bstats["saved_blocks"]++;
            bstats["saved_bytes"] += data.Length;

            try
            {
                var outputStream = File.Create(outFile);
                var bzip2OutputStream = new BZip2OutputStream(outputStream);
                bzip2OutputStream.Write(data.Select(x => (byte)x).ToArray());
                bzip2OutputStream.Close();
            }
            catch (Exception ex)
            {
                error(outFile, nameof(BZip2OutputStream), ex);
                packsum += new FileInfo(outFile).Length;
                return hash; // ???
            }

            packsum += new FileInfo(outFile).Length;
            if (TESTING) ConWrite(hash);
        }
        else
        {
            bstats["duplicate_blocks"]++;
            bstats["duplicate_bytes"] += data.Length;
            if (TESTING) ConWrite($"{hash} already exists");
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
                var data = new byte[CHUNKSIZE];
                var n12 = fileStream.Read(data, 0, (int)Math.Min(CHUNKSIZE, size));
                if (TESTING) ConWrite($"chunk: {Dumper(D(size), D(n12))}");
                if (n12 == 0) break;

                hashes.Add(save_data(new string(data.AsSpan(0, n12).ToArray().Select(x => (char)x).ToArray())));
                size -= n12;
                ds += n12;
            }
            catch (Exception ex)
            {
                error(tag, nameof(Stream.Read), ex);
            }

        if (TESTING) ConWrite($"eof: {Dumper(D(size), D(hashes))}");
        return hashes;
    }

    [LibraryImport("libc.so.6", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int lstat([MarshalAs(UnmanagedType.LPStr)] string __file, ref statInfo __buf);

    private static object[] lstat(string filename)
    {
        var buf = new statInfo();
        var ret = lstat(filename, ref buf);
        if (ret != 0) throw new Win32Exception();

        double t2d(timeSpec t)
        {
            return t.tv_sec + (double)t.tv_nsec / (1000 * 1000 * 1000);
        }

        return
        [
            buf.st_dev,
            buf.st_ino,
            buf.st_mode,
            buf.st_nlink,
            buf.st_uid,
            buf.st_gid,
            buf.st_rdev,
            buf.st_size,
            t2d(buf.st_atim),
            t2d(buf.st_mtim),
            t2d(buf.st_ctim),
            buf.st_blksize,
            buf.st_blocks
        ];
    }

    private static bool S_ISDIR(uint m)
    {
        return (m & 0170000) == 0040000;
    }

    private static bool S_ISDIR(statInfo s)
    {
        return S_ISDIR(s.st_mode);
    }

    private static bool S_ISDIR(object[] s)
    {
        return S_ISDIR((uint)s[2]);
    }

    private static bool S_ISREG(uint m)
    {
        return (m & 0170000) == 0100000;
    }

    private static bool S_ISREG(statInfo s)
    {
        return S_ISREG(s.st_mode);
    }

    private static bool S_ISREG(object[] s)
    {
        return S_ISREG((uint)s[2]);
    }

    private static bool S_ISLNK(uint m)
    {
        return (m & 0170000) == 0120000;
    }

    private static bool S_ISLNK(statInfo s)
    {
        return S_ISLNK(s.st_mode);
    }

    private static bool S_ISLNK(object[] s)
    {
        return S_ISLNK((uint)s[2]);
    }

    [LibraryImport("libc.so.6", StringMarshalling = StringMarshalling.Utf8)]
    private static partial long readlink([MarshalAs(UnmanagedType.LPStr)] string path,
        [MarshalAs(UnmanagedType.LPArray)] byte[] buf, ulong bufsize);

    private static string readlink(string path)
    {
        var sz = readlink(path, buf, (ulong)buf.Length);
        do
        {
            if (sz == -1) throw new Win32Exception();
            if (sz < buf.Length) break;
            buf = new byte[buf.Length * 2];
        } while (true);

        return new string(buf.AsSpan(0, (int)sz).ToArray().Select(x => (char)x).ToArray());
    }


    // ##############################################################################
    private static void backup_worker(string[] entries_)
    {
        foreach (var entry in entries_.OrderBy(e => e))
        {
            var volume = Path.GetPathRoot(entry);
            var directories = Path.GetDirectoryName(entry);
            var file = Path.GetFileName(entry);
            if (TESTING) ConWrite($"{"=".Repeat(80)}\n");
            if (TESTING) ConWrite($"{Dumper(D(entry), D(volume), D(directories), D(file))}");
            var dir = Path.Combine(volume ?? string.Empty, directories ?? string.Empty);
            var name = file;
            object[] statBuf = [];
            DateTime? start = null;
            try
            {
                statBuf = lstat(entry);

                // $dir is the current directory name,
                // $name is the current filename within that directory
                // $entry is the complete pathname to the file.
                start = DateTime.Now;
                if (TESTING) ConWrite($"handle_file: {Dumper(D(dir), D(name), D(entry))}");
            }
            catch (Exception ex)
            {
                error(entry, nameof(lstat), ex);
            }

            if (TESTING) ConWrite(Dumper(D(statBuf[0])));
            if (devices.ContainsKey((ulong)statBuf[0]) && data_path != null &&
                Path.GetRelativePath(data_path, entry).StartsWith(".."))
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
                var fsfid = sdpack((ulong[])[(ulong)statBuf[0], (ulong)statBuf[1]], "fsfid");
                var old = fs2ino.ContainsKey(fsfid);
                string report;
                if (!old)
                {
                    fs2ino[fsfid] = sdpack(null, "");
                    if (S_ISDIR(statBuf))
                        while (true)
                            try
                            {
                                var entries = Directory.GetFileSystemEntries(entry); // Assuming no . ..
                                if (TESTING) Console.Write($"\t{entries.Length} entries");
                                backup_worker(entries.Where(x => !x.StartsWith(".."))
                                    .Select(x => Path.Combine(entry, x)).ToArray());
                                if (TESTING) Console.WriteLine($"\tdone {entry}");
                            }
                            catch (Exception ex)
                            {
                                error(entry, nameof(Directory.GetFileSystemEntries), ex);
                            }

                    packsum = 0;
                    // lstat(entry);
                    var inode = new List<object[]>
                    {
                        new[] { statBuf[2], statBuf[3] },
                        usr((int)statBuf[4]),
                        grp((int)statBuf[5]),
                        new[] { statBuf[6], statBuf[7], statBuf[9], statBuf[10] }
                    }.ToArray();
                    string? data;
                    string[] hashes = [];
                    ds = 0;
                    if (S_ISREG(statBuf))
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
                                error(entry, nameof(File.OpenRead), ex);
                                continue;
                            }
                    }
                    else if (S_ISLNK(statBuf))
                    {
                        try
                        {
                            data = readlink(entry);
                        }
                        catch (Exception ex)
                        {
                            error(entry, nameof(readlink), ex);
                            continue;
                        }

                        var size = data.Length;
                        if (TESTING) ConWrite(Dumper(D(data)));
                        MemoryStream? mem1 = null;
                        try
                        {
                            var dataBytes = Encoding.UTF8.GetBytes(data);
                            mem1 = new MemoryStream(dataBytes);
                        }
                        catch (Exception ex)
                        {
                            error(entry, nameof(MemoryStream), ex);
                        }

                        // open my $mem, '<:unix mmap raw', \$data or die "\$data: $!";
                        hashes = save_file(mem1!, size, $"{entry} $data readlink").ToArray();
                        ds = data.Length;
                        data = null;
                    }
                    else if (S_ISDIR(statBuf))
                    {
                        var data2 = sdpack(dirtmp[entry] ?? [], "dir");
                        dirtmp.Remove(entry);
                        var size = data2.Length;
                        if (TESTING) ConWrite(Dumper(D(data2)));
                        MemoryStream? mem2 = null;
                        try
                        {
                            var dataBytes = Encoding.UTF8.GetBytes(data2);
                            mem2 = new MemoryStream(dataBytes);
                        }
                        catch (Exception ex)
                        {
                            error(entry, nameof(MemoryStream), ex);
                            continue;
                        }

                        // open my $mem, '<:unix mmap raw', \$data or die "\$data: $!";
                        hashes = save_file(mem2!, size, $"{entry} $data $dirtmp").ToArray();
                        ds = data2.Length;
                        data2 = null;
                    }

                    if (TESTING) ConWrite($"data: {Dumper(D(hashes))}");
                    inode = inode.Append(hashes).ToArray();
                    data = sdpack(inode, "inode");
                    if (TESTING) ConWrite(Dumper(D(data)));
                    MemoryStream? mem = null;
                    try
                    {
                        var dataBytes = Encoding.UTF8.GetBytes(data);
                        mem = new MemoryStream(dataBytes);
                    }
                    catch (Exception ex)
                    {
                        error(entry, nameof(MemoryStream), ex);
                        continue;
                    }

                    // open my $mem, '<:unix mmap raw scalar', \$data or die "\$data: $!";
                    hashes = save_file(mem!, data.Length, $"{entry} $data @inode").ToArray();
                    var ino = sdpack(hashes.ToArray(), "fileid");
                    fs2ino[fsfid] = ino;
                    TimeSpan? needed = start == null ? null : DateTime.Now.Subtract((DateTime)start);
                    var speed = needed?.TotalSeconds > 0 ? (double?)ds / needed.Value.TotalSeconds : null;
                    if (TESTING) ConWrite($"timing: {Dumper(D(ds), D(needed), D(speed))}");
                    report = $"[{statBuf[7]:d} -> {packsum:d}: {needed:d}s]";
                }
                else
                {
                    report = $"[{statBuf[7]:d} -> duplicate]";
                }

                if (!dirtmp.ContainsKey(dir)) dirtmp[dir] = new List<object>();
                if (fs2ino.TryGetValue(fsfid, out var fs2inoValue))
                    dirtmp[dir].Add(new object?[] { name, fs2inoValue });
                LOG?.Write(
                    $"{BitConverter.ToString(Encoding.UTF8.GetBytes(fs2ino[fsfid] ?? string.Empty)).Replace("-", "")} {entry} {report}\n");
                if (TESTING) ConWrite($"{"_".Repeat(80)}\n");
            }
            else
            {
                error(entry, "pruning");
            }

            if (TESTING) ConWrite($"{"_".Repeat(80)}\n");
        }
    }

    private static void LogLine(string message)
    {
        if (TESTING)
        {
            ConWrite(message);
            LOG?.WriteLine($"{DateTime.Now}: {message}");
        }
    }

    private static void LogError(string entry, string message)
    {
        error($"Error: {entry} - {message}", "?");
        LOG?.WriteLine($"Error: {entry} - {message}");
    }


    private readonly struct passwdEntry
    {
        public readonly string pw_name = "";
        public readonly string pw_passwd = "";
        public readonly uint pw_uid = new();
        public readonly uint pw_gid = new();
        public readonly string pw_gecos = "";
        public readonly string pw_dir = "";
        public readonly string pw_shell = "";

        public passwdEntry()
        {
        }
    }

    private readonly struct groupEntry
    {
        public readonly string gr_name = "";
        public readonly string gr_passwd = "";
        public readonly uint gr_gid = new();
        public readonly string[] gr_mem = new string[0];

        public groupEntry()
        {
        }
    }


    private readonly struct timeSpec
    {
        public readonly long tv_sec = new();
        public readonly long tv_nsec = new();

        public timeSpec()
        {
        }
    }

    private readonly struct statInfo
    {
        public readonly ulong st_dev = new();
        public readonly ulong st_ino = new();
        public readonly ulong st_nlink = new();
        public readonly uint st_mode = new();
        public readonly uint st_uid = new();
        public readonly uint st_gid = new();
        private readonly int __pad0 = new();
        public readonly ulong st_rdev = new();
        public readonly long st_size = new();
        public readonly long st_blksize = new();
        public readonly long st_blocks = new();
        public readonly timeSpec st_atim = new();
        public readonly timeSpec st_mtim = new();
        public readonly timeSpec st_ctim = new();
        private readonly long __glibc_reserved1 = new();
        private readonly long __glibc_reserved2 = new();
        private readonly long __glibc_reserved3 = new();

        public statInfo()
        {
        }
    }
}
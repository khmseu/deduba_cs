using System.Collections;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.BZip2;
using OsCalls;
using UtilitiesLibrary;

namespace DeDuBa;

// ReSharper disable once ClassNeverInstantiated.Global
public class DedubaClass
{
    private const long Chunksize = 1024 * 1024 * 1024;

    private static string? _startTimestamp;
    private static string? _archive;

    private static string _dataPath = "";

    // private static string? _tmpp;

    // private static readonly Dictionary<string, string> Settings = new();
    private static long _ds;
    private static readonly Dictionary<string, List<object>> Dirtmp = new();
    private static readonly Dictionary<string, long> Bstats = [];
    private static readonly Dictionary<ulong, int> Devices = [];
    private static readonly Dictionary<string, string?> Fs2Ino = [];
    private static long _packsum;

    // ############################################################################
    // Temporary on-disk hashes for backup data management
    // ############################################################################
    // arlist: hash -> part of filename between $data_path and actual file
    private static readonly Dictionary<string, string> Arlist = [];

    // preflist: list of files and directories under a given prefix
    // (as \0-separated list)
    private static readonly Dictionary<string, string> Preflist = [];

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
    private static double[]? Ls2Od(JsonNode? ls)
    {
        if (ls == null)
            return null;
        var od = new double[13];
        od[0] = ls["st_dev"]?.GetValue<ulong>() ?? 0;
        od[1] = ls["st_ino"]?.GetValue<ulong>() ?? 0;
        od[2] = ls["st_mode"]?.GetValue<ulong>() ?? 0;
        od[3] = ls["st_nlink"]?.GetValue<ulong>() ?? 0;
        od[4] = ls["st_uid"]?.GetValue<ulong>() ?? 0;
        od[5] = ls["st_gid"]?.GetValue<ulong>() ?? 0;
        od[6] = ls["st_rdev"]?.GetValue<ulong>() ?? 0;
        od[7] = ls["st_size"]?.GetValue<ulong>() ?? 0;
        od[8] = ls["st_atim"]?.GetValue<double>() ?? 0;
        od[9] = ls["st_mtim"]?.GetValue<double>() ?? 0;
        od[10] = ls["st_ctim"]?.GetValue<double>() ?? 0;
        od[11] = ls["st_blksize"]?.GetValue<ulong>() ?? 0;
        od[12] = ls["st_blocks"]?.GetValue<ulong>() ?? 0;
        return od;
    }

    public static void Backup(string[] argv)
    {
        _startTimestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

        _archive = Utilities.Testing ? "/home/kai/projects/Backup/ARCHIVE3" : "/archive/backup";
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
                Utilities._log = new StreamWriter(logname);
            }
            catch (Exception ex)
            {
                Utilities.Error(logname, nameof(StreamWriter), ex);
                throw;
            } // STDOUT->autoflush(1);

            // STDERR->autoflush(1);
            Utilities._log.AutoFlush = true;

            //#############################################################################
            // Main program
            //#############################################################################
            Console.Write("\n\nMain program\n");

            try
            {
                // @ARGV = map { canonpath realpath $_ } @ARGV;
                try
                {
                    argv = argv.Select(FileSystem.Canonicalizefilename)
                        .Select(node => node["path"]?.ToString())
                        .Select(path => path != null ? Path.GetFullPath(path) : "")
                        .ToArray();
                }
                catch (Exception ex)
                {
                    Utilities.Error(nameof(argv), nameof(FileSystem.Canonicalizefilename), ex);
                    throw;
                }

                Utilities.ConWrite($"Filtered: {Utilities.Dumper(Utilities.D(argv))}");

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

                    var i = st["st_dev"]?.GetValue<ulong>() ?? 0;
                    Devices.TryAdd(i, 0);
                    Devices[i]++;
                }

                Utilities.ConWrite(Utilities.Dumper(Utilities.D(Devices)));

                // ############################################################################

                // tie % arlist,   'DB_File', undef;    #, "$tmpp.arlist";
                // tie % preflist, 'DB_File', undef;    #, "$tmpp.preflist";
                Preflist[""] = "";

                Utilities.ConWrite("Getting archive state\n");

                Mkarlist(_dataPath);

                if (Utilities.Testing)
                {
                    Utilities.ConWrite("Before backup:\n");
                    foreach (var kvp in Arlist)
                        Utilities.ConWrite(
                            Utilities.Dumper(
                                new KeyValuePair<string, object?>(
                                    $"{nameof(Arlist)}['{kvp.Key}']",
                                    kvp.Value
                                )
                            )
                        );

                    // Iterate over preflist
                    foreach (var kvp in Preflist)
                        Utilities.ConWrite(
                            Utilities.Dumper(
                                new KeyValuePair<string, object?>(
                                    $"{nameof(Preflist)}['{kvp.Key}']",
                                    kvp.Value
                                )
                            )
                        );
                }

                // ##############################################################################

                Utilities.ConWrite("Backup starting\n");

                backup_worker(argv);

                // ##############################################################################

                // # my $status = unxz $input => $output [,OPTS] or print "\n", __LINE__, ' ', scalar localtime, ' ',  "\n", __ "unxz failed: $UnXzError\n" if TESTING;

                if (Utilities.Testing)
                    Utilities.ConWrite(Utilities.Dumper(Utilities.D(Dirtmp)));

                if (Utilities.Testing)
                    Utilities.ConWrite(Utilities.Dumper(Utilities.D(Devices)));

                Utilities.ConWrite(Utilities.Dumper(Utilities.D(Bstats)));

                // untie %arlist;
                // untie %preflist;
                // unlink "$tmpp.arlist", "$tmpp.preflist";

                Utilities._log?.Close();

                Utilities.ConWrite("Backup done\n");
            }
            catch (Exception ex)
            {
                Utilities.Error(logname, nameof(Utilities._log.Close), ex);
            }
        }
        finally
        {
            Utilities._log?.Close();
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

    private static void Mkarlist(params string[] filePaths)
    {
        foreach (var entry in filePaths.OrderBy(e => e))
        {
            if (entry == _dataPath)
            {
                if (Utilities.Testing)
                    Utilities.ConWrite($"+ {entry}");
                try
                {
                    var entries = Directory.GetFileSystemEntries(entry); // Assuming no . ..
                    if (Utilities.Testing)
                        Console.Write($"\t{entries.Length} entries");
                    Mkarlist(entries);
                    if (Utilities.Testing)
                        Console.WriteLine($"\tdone {entry}");
                }
                catch (Exception ex)
                {
                    Utilities.Error(entry, nameof(Directory.GetFileSystemEntries), ex);
                }

                continue;
            }

            var match = Regex.Match(entry, $"^{Regex.Escape(_dataPath)}/?(.*)/([^/]+)$");
            var prefix = match.Groups[1].Value;
            var file = match.Groups[2].Value;
            if (Regex.IsMatch(file, "^[0-9a-f][0-9a-f]$"))
            {
                if (Utilities.Testing)
                    Utilities.ConWrite($"+ {entry}:{prefix}:{file}");
                Preflist.TryAdd(prefix, "");
                Preflist[prefix] += $"{file}/\0";
                try
                {
                    var dirEntries = Directory.GetFileSystemEntries(entry); // Assuming no . ..
                    if (Utilities.Testing)
                        Console.Write($"\t{dirEntries.Length} entries");
                    Mkarlist(dirEntries);
                    if (Utilities.Testing)
                        Console.WriteLine($"\tdone {entry}");
                }
                catch (Exception ex)
                {
                    Utilities.Error(entry, nameof(Directory.GetFileSystemEntries), ex);
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
                Utilities.Warn($"Bad entry in archive: {entry}");
            }
        }
    }

    // ############################################################################
    // find place for hashed file, or note we already have it

    private static string? Hash2Fn(string hash)
    {
        if (Utilities.Testing)
            Utilities.ConWrite(Utilities.Dumper(Utilities.D(hash)));

        if (Arlist.TryGetValue(hash, out var value))
        {
            _packsum += new FileInfo(Path.Combine(_dataPath, value, hash)).Length;
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
            if (Preflist.ContainsKey(prefix))
                break;
            prefixList.RemoveAt(prefixList.Count - 1);
        }

        if (prefixList.Count == 0)
            prefix = string.Join("/", prefixList);
        var list = Preflist[prefix].Split('\0').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var nlist = list.Count;

        if (nlist > 255)
        {
            // dir becoming too large, move files into subdirs
            Utilities.ConWrite($"*** reorganizing '{prefix}' [{nlist} entries]\n");
            if (Utilities.Testing)
                Utilities.ConWrite($"{Utilities.Dumper(Utilities.D(list))}\n");

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
                        Utilities.Error($"{from} -> {to}", nameof(File.Move), ex);
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
            // print "\n", __LINE__, ' ', scalar localtime, ' ',  Data::Utilities.Dumper->Dump([$v], ["\$arlist{'$k'}"]) if TESTING;
            //           }
            // while (my ($k, $v) = each %preflist) {
            // print "\n", __LINE__, ' ', scalar localtime, ' ',  Data::Utilities.Dumper->Dump([$v], ["\$preflist{'$k'}"]) if TESTING;
            //           }
            // print "\n", __LINE__, ' ', scalar localtime, ' ',  "\n" if TESTING;
        }
        else
        {
            if (Utilities.Testing)
                Utilities.ConWrite($"+++ not too large: '{prefix}' entries = {list.Count}\n");
        }

        Arlist[hash] = prefix;
        Preflist.TryAdd(prefix, "");
        Preflist[prefix] += $"{hash}\0";
        return Path.Combine(_dataPath, prefix, hash);
    }

    private static string pack_w<TNum>(TNum value)
        where TNum : INumber<TNum>
    {
        if (TNum.IsNegative(value))
            throw new InvalidOperationException(
                "Cannot compress negative numbers in " + nameof(pack_w)
            );
        var buf = new byte[Marshal.SizeOf(typeof(TNum)) * 8 / 7 + 1];
        var inIndex = buf.Length;
        do
        {
            buf[--inIndex] = (byte)(((dynamic)value & 0x7F) | 0x80);
            value = (TNum)((dynamic)value >> 7);
        } while ((dynamic)value > 0);

        buf[^1] &= 0x7F; /* clear continue bit */
        return new string(buf.Skip(inIndex).Select(b => (char)b).ToArray());
    }

    private static ulong unpack_w(string value, ref int s)
    {
        ulong auv = 0;
        while (s < value.Length)
        {
            var ch = (byte)value[s++];
            auv = (auv << 7) | ((ulong)ch & 0x7f);
            if (ch < 0x80)
                return auv;
        }

        throw new InvalidOperationException(
            "Unterminated compressed integer in " + nameof(unpack_w)
        );
    }

    private static ulong unpack_w(string value)
    {
        var s = 0;
        var ret = unpack_w(value, ref s);
        if (s < value.Length)
            throw new InvalidOperationException(
                "Junk after compressed integer in " + nameof(unpack_w)
            );
        return ret;
    }

    // ############################################################################
    // Structured data
    //
    // unpacked: [ ... [ ... ] ... 'string' \number ... ]
    //
    // packed: w/a strings, w/w unsigned numbers, w/(a) lists
    //

    // Überladung für Nullable-Typen
    private static string SdpackNull(object? v, string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        var t = v?.GetType();
        if (name.Length > 0 && Utilities.Testing)
            Utilities.ConWrite(
                $"{name}: {Utilities.Dumper(Utilities.D(t?.FullName), Utilities.D(v))}"
            );
        return v is null ? "u" : Sdpack(v, name);
    }

    // Überladung für Stringtypen
    private static string SdpackString(string v, string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        var t = v.GetType();
        if (name.Length > 0 && Utilities.Testing)
            Utilities.ConWrite(
                $"{name}: {Utilities.Dumper(Utilities.D(t.FullName), Utilities.D(v))}"
            );

        return "s" + v;
    }

    // Überladung für nicht-String-Werttypen
    private static string SdpackNum<T>(T v, string name)
        where T : struct
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        var t = v.GetType();
        if (name.Length > 0 && Utilities.Testing)
            Utilities.ConWrite(
                $"{name}: {Utilities.Dumper(Utilities.D(t.FullName), Utilities.D(v))}"
            );

        var intValue = Convert.ToInt64(v);
        return intValue >= 0 ? "n" + pack_w((ulong)intValue) : "N" + pack_w((ulong)-intValue);
    }

    // Überladung für Enumerables
    private static string SdpackSeq<T>(T v, string name)
        where T : IEnumerable
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        var t = v.GetType();
        if (name.Length > 0 && Utilities.Testing)
            Utilities.ConWrite(
                $"{name}: {Utilities.Dumper(Utilities.D(t.FullName), Utilities.D(v))}"
            );

        var ary = new List<string>();
        foreach (var item in v)
            ary.Add(Sdpack(item, ""));
        return "l"
            + pack_w((ulong)ary.Count)
            + string.Join("", ary.Select(x => pack_w((ulong)x.Length) + x));
    }

    // Überladung für Objekte (Fallback)
    private static string SdpackOther(object? v, string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        var t = v?.GetType();

        throw new ArgumentException($"unexpected type {t?.FullName ?? "unknown"}", name);
    }

    private static string Sdpack(object? v, string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        var t = v?.GetType();
        if (name.Length > 0 && Utilities.Testing)
            Utilities.ConWrite(
                $"{name}: {Utilities.Dumper(Utilities.D(t?.FullName), Utilities.D(v))}"
            );

        switch (v)
        {
            case null:
                return SdpackNull(v, name);
            case string s:
                return SdpackString(s, name);
            case Int128 int128:
                return SdpackNum(int128, name);
            case long int64:
                return SdpackNum(int64, name);
            case int int32:
                return SdpackNum(int32, name);
            case short int16:
                return SdpackNum(int16, name);
            case sbyte int8:
                return SdpackNum(int8, name);
            case UInt128 uint128:
                return SdpackNum(uint128, name);
            case ulong uint64:
                return SdpackNum(uint64, name);
            case uint uint32:
                return SdpackNum(uint32, name);
            case ushort uint16:
                return SdpackNum(uint16, name);
            case byte uint8:
                return SdpackNum(uint8, name);
            case double @double:
                return SdpackNum(@double, name);
            case IEnumerable en:
                return SdpackSeq(en, name);
            default:
                return SdpackOther(v, name);
        }
    }

    // ReSharper disable once UnusedMember.Global
    private static object? Sdunpack(string value)
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
                while (n-- > 0)
                {
                    var il = unpack_w(d, ref s);
                    unpackedList.Add(Sdunpack(d.Substring(s, (int)il)));
                }

                if (s < d.Length)
                    throw new InvalidOperationException(
                        "Junk after compressed integer in " + nameof(Sdunpack)
                    );
                return unpackedList.ToArray();
            default:
                throw new InvalidOperationException("unexpected type " + p);
        }
    }

    private static object[] Usr(uint uid)
    {
        return [uid, UserGroupDatabase.GetPwUid(uid)["pw_name"]?.ToString() ?? uid.ToString()];
    }

    private static object[] Grp(uint gid)
    {
        return [gid, UserGroupDatabase.GetGrGid(gid)["gr_name"]?.ToString() ?? gid.ToString()];
    }

    private static string save_data(string data)
    {
        var hashBytes = SHA512.HashData(data.ToArray().Select(x => (byte)x).ToArray());
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        var outFile = Hash2Fn(hash);

        if (outFile != null)
        {
            Bstats.TryAdd("saved_blocks", 0);
            Bstats["saved_blocks"]++;
            Bstats.TryAdd("saved_bytes", 0);
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
                Utilities.Error(outFile, nameof(BZip2OutputStream), ex);
                _packsum += new FileInfo(outFile).Length;
                return hash; // ???
            }

            _packsum += new FileInfo(outFile).Length;
            if (Utilities.Testing)
                Utilities.ConWrite(hash);
        }
        else
        {
            Bstats.TryAdd("duplicate_blocks", 0);
            Bstats["duplicate_blocks"]++;
            Bstats.TryAdd("duplicate_bytes", 0);
            Bstats["duplicate_bytes"] += data.Length;
            if (Utilities.Testing)
                Utilities.ConWrite($"{hash} already exists");
        }

        return hash;
    }

    private static List<string> save_file(Stream fileStream, long size, string tag)
    {
        if (Utilities.Testing)
            Utilities.ConWrite(
                $"save_file: {Utilities.Dumper(Utilities.D(size), Utilities.D(tag))}"
            );
        var hashes = new List<string>();

        // my @layers = PerlIO::get_layers($file, details => 1);
        // print "\n", __LINE__, ' ', scalar localtime, ' input: ', Utilities.Dumper(@layers, $size) if TESTING;
        while (size > 0)
            try
            {
                var data = new byte[Chunksize];
                var n12 = fileStream.Read(data, 0, (int)Math.Min(Chunksize, size));
                if (Utilities.Testing)
                    Utilities.ConWrite(
                        $"chunk: {Utilities.Dumper(Utilities.D(size), Utilities.D(n12))}"
                    );
                if (n12 == 0)
                    break;

                hashes.Add(
                    save_data(
                        new string(data.AsSpan(0, n12).ToArray().Select(x => (char)x).ToArray())
                    )
                );
                size -= n12;
                _ds += n12;
            }
            catch (Exception ex)
            {
                Utilities.Error(tag, nameof(Stream.Read), ex);
            }

        if (Utilities.Testing)
            Utilities.ConWrite($"eof: {Utilities.Dumper(Utilities.D(size), Utilities.D(hashes))}");
        return hashes;
    }

    // ##############################################################################
    private static void backup_worker(string[] filesToBackup)
    {
        Utilities.ConWrite(
            $"Debug 1: {nameof(backup_worker)}({Utilities.Dumper(Utilities.D(filesToBackup))})"
        );
        Utilities.ConWrite(
            $"Debug 2: {nameof(backup_worker)}({Utilities.Dumper(Utilities.D(filesToBackup.OrderBy(e => e, StringComparer.Ordinal)))})"
        );
        foreach (var entry in filesToBackup.OrderBy(e => e, StringComparer.Ordinal))
        {
            var volume = Path.GetPathRoot(entry);
            var directories = Path.GetDirectoryName(entry);
            var file = Path.GetFileName(entry);
            if (Utilities.Testing)
                Utilities.ConWrite($"{"=".Repeat(80)}\n");
            if (Utilities.Testing)
                Utilities.ConWrite(
                    $"{Utilities.Dumper(Utilities.D(entry), Utilities.D(volume), Utilities.D(directories), Utilities.D(file))}"
                );
            var dir = Path.Combine(volume ?? string.Empty, directories ?? string.Empty);
            var name = file;
            JsonNode? statBuf = null;

            // $dir is the current directory name,
            // $name is the current filename within that directory
            // $entry is the complete pathname to the file.
            var start = DateTime.Now;
            if (Utilities.Testing)
                Utilities.ConWrite(
                    $"handle_file: {Utilities.Dumper(Utilities.D(dir), Utilities.D(name), Utilities.D(entry))}"
                );
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

            var stDev = statBuf?["st_dev"]?.GetValue<ulong>() ?? 0;
            if (Utilities.Testing)
                Utilities.ConWrite(Utilities.Dumper(Utilities.D(stDev)));
            if (
                Devices.ContainsKey(stDev)
                && _dataPath != null
                && Path.GetRelativePath(_dataPath, entry).StartsWith("..")
            )
            {
                Utilities.ConWrite($"stat: {Utilities.Dumper(Utilities.D(Ls2Od(statBuf)))}");

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
                var fsfid = Sdpack(
                    new List<object?>
                    {
                        statBuf?["st_dev"]?.GetValue<ulong>() ?? 0,
                        statBuf?["st_ino"]?.GetValue<ulong>(),
                    },
                    "fsfid"
                );
                var old = Fs2Ino.ContainsKey(fsfid);
                string report;
                if (!old)
                {
                    Fs2Ino[fsfid] = Sdpack(null, "");
                    if (FileSystem.IsDir(statBuf))
                        while (true)
                            try
                            {
                                var entries = Directory.GetFileSystemEntries(entry); // Assuming no . ..
                                if (Utilities.Testing)
                                    Console.Write($"\t{entries.Length} entries");
                                backup_worker(
                                    entries
                                        .Where(x => !x.StartsWith(".."))
                                        .Select(x => Path.Combine(entry, x))
                                        .ToArray()
                                );
                                if (Utilities.Testing)
                                    Console.WriteLine($"\tdone {entry}");
                            }
                            catch (Exception ex)
                            {
                                Utilities.Error(entry, nameof(Directory.GetFileSystemEntries), ex);
                            }

                    _packsum = 0;
                    // lstat(entry);
                    var filteredInode = Ls2Od(statBuf);
                    var inode = new List<object>
                    {
                        new object?[] { filteredInode?[2], filteredInode?[3] },
                        Usr(Convert.ToUInt32(filteredInode?[4])),
                        Grp(Convert.ToUInt32(filteredInode?[5])),
                        new object?[]
                        {
                            filteredInode?[6],
                            filteredInode?[7],
                            filteredInode?[9],
                            filteredInode?[10],
                        },
                    };
                    string[] hashes = [];
                    _ds = 0;
                    MemoryStream mem;
                    if (FileSystem.IsReg(statBuf))
                    {
                        // ReSharper disable once ConstantConditionalAccessQualifier
                        var size = statBuf?["st_size"]?.GetValue<ulong>();
                        if (size != 0)
                            try
                            {
                                if (Utilities.Testing)
                                    Utilities.ConWrite(Utilities.Dumper(Utilities.D(entry)));
                                var fileStream = File.OpenRead(entry);
                                hashes = save_file(fileStream, (long?)size ?? 0, entry).ToArray();
                            }
                            catch (Exception ex)
                            {
                                Utilities.Error(entry, nameof(File.OpenRead), ex);
                                continue;
                            }
                    }
                    else if (FileSystem.IsLnk(statBuf))
                    {
                        string? dataIslink;
                        try
                        {
                            dataIslink = FileSystem.ReadLink(entry).GetValue<string>();
                        }
                        catch (Exception ex)
                        {
                            Utilities.Error(entry, nameof(FileSystem.ReadLink), ex);
                            continue;
                        }

                        var size = dataIslink.Length;
                        if (Utilities.Testing)
                            Utilities.ConWrite(Utilities.Dumper(Utilities.D(dataIslink)));
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
                        hashes = save_file(mem1!, size, $"{entry} $data readlink").ToArray();
                        _ds = dataIslink.Length;
                    }
                    else if (FileSystem.IsDir(statBuf))
                    {
                        var dataIsdir = Sdpack(
                            Dirtmp.TryGetValue(entry, out var value) ? value : new List<object>(),
                            "dir"
                        );
                        Dirtmp.Remove(entry);
                        var size = dataIsdir.Length;
                        if (Utilities.Testing)
                            Utilities.ConWrite(Utilities.Dumper(Utilities.D(dataIsdir)));
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
                        hashes = save_file(mem, size, $"{entry} $data $dirtmp").ToArray();
                        _ds = dataIsdir.Length;
                    }

                    if (Utilities.Testing)
                        Utilities.ConWrite($"data: {Utilities.Dumper(Utilities.D(hashes))}");
                    inode = inode.Append(hashes).ToList();
                    var data = Sdpack(inode, "inode");
                    if (Utilities.Testing)
                        Utilities.ConWrite(Utilities.Dumper(Utilities.D(data)));
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
                    hashes = save_file(mem, data.Length, $"{entry} $data @inode").ToArray();
                    var ino = Sdpack(hashes.ToArray(), "fileid");
                    Fs2Ino[fsfid] = ino;
                    TimeSpan? needed = DateTime.Now.Subtract(start);
                    var speed =
                        needed.Value.TotalSeconds > 0
                            ? (double?)_ds / needed.Value.TotalSeconds
                            : null;
                    if (Utilities.Testing)
                        Utilities.ConWrite(
                            $"timing: {Utilities.Dumper(Utilities.D(_ds), Utilities.D(needed), Utilities.D(speed))}"
                        );
                    report =
                        $"[{statBuf?["st_size"]?.GetValue<ulong>():d} -> {_packsum:d}: {needed:c}s]";
                }
                else
                {
                    report = $"[{statBuf?["st_size"]?.GetValue<ulong>():d} -> duplicate]";
                }

                if (!Dirtmp.ContainsKey(dir))
                    Dirtmp[dir] = new List<object>();
                if (Fs2Ino.TryGetValue(fsfid, out var fs2InoValue))
                    Dirtmp[dir].Add(new object?[] { name, fs2InoValue });
                Utilities._log?.Write(
                    $"{BitConverter.ToString(Encoding.UTF8.GetBytes(Fs2Ino[fsfid] ?? string.Empty)).Replace("-", "")} {entry} {report}\n"
                );
                if (Utilities.Testing)
                    Utilities.ConWrite($"{"_".Repeat(80)}\n");
            }
            else
            {
                Utilities.Error(entry, "pruning");
            }

            if (Utilities.Testing)
                Utilities.ConWrite($"{"_".Repeat(80)}\n");
        }
    }
}

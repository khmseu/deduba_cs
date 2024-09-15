using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DeDuBa;

public partial class LibCalls
{
    [LibraryImport("libc.so.6")]
    private static partial IntPtr getpwuid(uint uid);

    [LibraryImport("libc.so.6")]
    private static partial IntPtr getgrgid(uint gid);

    [LibraryImport("libc.so.6", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int __lxstat(int __ver, string __filename, ref StatInfo __stat_buf);


    [LibraryImport("libc.so.6", StringMarshalling = StringMarshalling.Utf8)]
    private static partial long __readlink_alias([MarshalAs(UnmanagedType.LPStr)] string path,
        [MarshalAs(UnmanagedType.LPArray)] byte[] buf, ulong bufsize);

    public static bool S_ISDIR(uint m)
    {
        return (m & 0170000) == 0040000;
    }

    public static bool S_ISDIR(StatInfo s)
    {
        return S_ISDIR(s.StMode);
    }

    public static bool S_ISDIR(object[] s)
    {
        return S_ISDIR((uint)s[2]);
    }

    public static bool S_ISREG(uint m)
    {
        return (m & 0170000) == 0100000;
    }

    public static bool S_ISREG(StatInfo s)
    {
        return S_ISREG(s.StMode);
    }

    public static bool S_ISREG(object[] s)
    {
        return S_ISREG((uint)s[2]);
    }

    public static bool S_ISLNK(uint m)
    {
        return (m & 0170000) == 0120000;
    }

    public static bool S_ISLNK(StatInfo s)
    {
        return S_ISLNK(s.StMode);
    }

    public static bool S_ISLNK(object[] s)
    {
        return S_ISLNK((uint)s[2]);
    }


    [StructLayout(LayoutKind.Sequential)]
    public readonly struct StatInfo
    {
        public readonly ulong StDev = new();
        public readonly ulong StIno = new();
        public readonly ulong StNlink = new();
        public readonly uint StMode = new();
        public readonly uint StUid = new();
        public readonly uint StGid = new();
        private readonly int __pad0 = new();
        public readonly ulong StRdev = new();
        public readonly long StSize = new();
        public readonly long StBlksize = new();
        public readonly long StBlocks = new();
        public readonly TimeSpec StAtim = new();
        public readonly TimeSpec StMtim = new();
        public readonly TimeSpec StCtim = new();
        private readonly long __glibc_reserved1 = new();
        private readonly long __glibc_reserved2 = new();
        private readonly long __glibc_reserved3 = new();

        public StatInfo()
        {
        }
    }

    public readonly struct PasswdEntry
    {
        public readonly string PwName = "";
        public readonly string PwPasswd = "";
        public readonly uint PwUid = new();
        public readonly uint PwGid = new();
        public readonly string PwGecos = "";
        public readonly string PwDir = "";
        public readonly string PwShell = "";

        public PasswdEntry()
        {
        }
    }

    public readonly struct GroupEntry
    {
        public readonly string GrName = "";
        public readonly string gr_passwd = "";
        public readonly uint gr_gid = new();
        public readonly string[] GrMem = [];

        public GroupEntry()
        {
        }
    }


    public readonly struct TimeSpec
    {
        public readonly long TvSec = new();
        public readonly long TvNsec = new();

        public TimeSpec()
        {
        }
    }

    public static LibCalls.PasswdEntry GetPasswd(uint uid)
    {
        var pwPtr = LibCalls.getpwuid(uid);
        if (pwPtr == IntPtr.Zero) throw new Exception("Failed to get passwd struct");

        return Marshal.PtrToStructure<LibCalls.PasswdEntry>(pwPtr);
    }


    public static LibCalls.GroupEntry GetGroup(uint gid)
    {
        var grPtr = LibCalls.getgrgid(gid);
        if (grPtr == IntPtr.Zero) throw new Exception("Failed to get group struct");

        return Marshal.PtrToStructure<LibCalls.GroupEntry>(grPtr);
    }

    public static object[] Lstat(string filename)
    {
        var buf = new LibCalls.StatInfo();
        var ret = __lxstat(1, filename, ref buf);
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

    public static string Readlink(string path)
    {
        var sz = LibCalls.__readlink_alias(path, _buf, (ulong)_buf.Length);
        do
        {
            if (sz == -1) throw new Win32Exception();
            if (sz < _buf.Length) break;
            _buf = new byte[_buf.Length * 2];
        } while (true);

        return new string(_buf.AsSpan(0, (int)sz).ToArray().Select(x => (char)x).ToArray());
    }







    private static byte[] _buf = new byte[1];
}


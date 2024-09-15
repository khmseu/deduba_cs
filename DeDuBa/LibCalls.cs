using System.Runtime.InteropServices;

namespace DeDuBa;

public partial class LibCalls
{
    [LibraryImport("libc.so.6")]
    public static partial IntPtr getpwuid(uint uid);

    [LibraryImport("libc.so.6")]
    public static partial IntPtr getgrgid(uint gid);

    [DllImport("libc.so.6", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern int lstat(string file, ref StatInfo buf);

    [LibraryImport("libc.so.6", StringMarshalling = StringMarshalling.Utf8)]
    public static partial long readlink([MarshalAs(UnmanagedType.LPStr)] string path,
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
        public readonly ulong StDev;
        public readonly ulong StIno;
        public readonly ulong StNlink;
        public readonly uint StMode;
        public readonly uint StUid;
        public readonly uint StGid;
        private readonly int __pad0;
        public readonly ulong StRdev;
        public readonly long StSize;
        public readonly long StBlksize;
        public readonly long StBlocks;
        public readonly TimeSpec StAtim;
        public readonly TimeSpec StMtim;
        public readonly TimeSpec StCtim;
        private readonly long __glibc_reserved1;
        private readonly long __glibc_reserved2;
        private readonly long __glibc_reserved3;

        public StatInfo(
            ulong stDev,
            ulong stIno,
            ulong stNlink,
            uint stMode,
            uint stUid,
            uint stGid,
            int pad0,
            ulong stRdev,
            long stSize,
            long stBlksize,
            long stBlocks,
            TimeSpec stAtim,
            TimeSpec stMtim,
            TimeSpec stCtim,
            long glibcReserved1,
            long glibcReserved2,
            long glibcReserved3)
        {
            StDev = stDev;
            StIno = stIno;
            StNlink = stNlink;
            StMode = stMode;
            StUid = stUid;
            StGid = stGid;
            __pad0 = pad0;
            StRdev = stRdev;
            StSize = stSize;
            StBlksize = stBlksize;
            StBlocks = stBlocks;
            StAtim = stAtim;
            StMtim = stMtim;
            StCtim = stCtim;
            __glibc_reserved1 = glibcReserved1;
            __glibc_reserved2 = glibcReserved2;
            __glibc_reserved3 = glibcReserved3;
        }
    }
    public readonly struct PasswdEntry
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
}
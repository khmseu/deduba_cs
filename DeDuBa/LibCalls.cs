using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DeDuBa;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
// ReSharper disable InconsistentNaming
// ReSharper disable once ClassNeverInstantiated.Global
public partial class LibCalls
{
    private static byte[] _buf = new byte[1];

    [LibraryImport("libc.so.6")]
    private static partial IntPtr getpwuid(uint uid);

    [LibraryImport("libc.so.6")]
    private static partial IntPtr getgrgid(uint gid);

    [LibraryImport("libc.so.6", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int __lxstat(int __ver, string __filename, ref StatInfo __stat_buf);


    [LibraryImport("libc.so.6", StringMarshalling = StringMarshalling.Utf8)]
    private static partial long __readlink_alias([MarshalAs(UnmanagedType.LPStr)] string path,
        [MarshalAs(UnmanagedType.LPArray)] byte[] buf, ulong bufsize);

    private static bool S_ISDIR(uint m)
    {
        return (m & 0170000) == 0040000;
    }

    private static bool S_ISDIR(StatInfo s)
    {
        return S_ISDIR(s.StMode);
    }

    // ReSharper disable once UnusedMember.Local
    private static bool S_ISDIR(object[] s)
    {
        return S_ISDIR((uint)s[2]);
    }

    private static bool S_ISREG(uint m)
    {
        return (m & 0170000) == 0100000;
    }
    // ReSharper disable once UnusedMember.Local

    private static bool S_ISREG(StatInfo s)
    {
        return S_ISREG(s.StMode);
    }

    // ReSharper disable once UnusedMember.Local
    private static bool S_ISREG(object[] s)
    {
        return S_ISREG((uint)s[2]);
    }

    private static bool S_ISLNK(uint m)
    {
        return (m & 0170000) == 0120000;
    }

    // ReSharper disable once UnusedMember.Local
    private static bool S_ISLNK(StatInfo s)
    {
        return S_ISLNK(s.StMode);
    }

    // ReSharper disable once UnusedMember.Local
    private static bool S_ISLNK(object[] s)
    {
        return S_ISLNK((uint)s[2]);
    }

    public static PasswdEntry GetPasswd(uint uid)
    {
        var pwPtr = getpwuid(uid);
        if (pwPtr == IntPtr.Zero) throw new Exception("Failed to get passwd struct");

        return Marshal.PtrToStructure<PasswdEntry>(pwPtr);
    }


    public static GroupEntry GetGroup(uint gid)
    {
        var grPtr = getgrgid(gid);
        if (grPtr == IntPtr.Zero) throw new Exception("Failed to get group struct");

        return Marshal.PtrToStructure<GroupEntry>(grPtr);
    }

    public static LStatData? Lstat(string filename)
    {
        var buf = new StatInfo();
        var ret = __lxstat(1, filename, ref buf);
        if (ret != 0)
        {
            DedubaClass.Error(filename, nameof(__lxstat));
            return null;
        }

        double T2d(TimeSpec t)
        {
            return t.TvSec + (double)t.TvNsec / (1000 * 1000 * 1000);
        }

        return new LStatData(buf.StDev, buf.StIno, buf.StMode, S_ISDIR(buf), S_ISREG(buf), S_ISLNK(buf), buf.StNlink,
            buf.StUid, buf.StGid, buf.StRdev, buf.StSize,
            DateTime.UnixEpoch.AddSeconds(T2d(buf.StAtim)),
            DateTime.UnixEpoch.AddSeconds(T2d(buf.StMtim)),
            DateTime.UnixEpoch.AddSeconds(T2d(buf.StCtim)), buf.stBlksize, buf.StBlocks);
        // [
        //     buf.StDev,
        //     buf.StIno,
        //     buf.StMode,
        //     buf.StNlink,
        //     buf.StUid,
        //     buf.StGid,
        //     buf.StRdev,
        //     buf.StSize,
        //     T2d(buf.StAtim),
        //     T2d(buf.StMtim),
        //     T2d(buf.StCtim),
        //     buf.stBlksize,
        //     buf.StBlocks
        // ];
    }

    public static string Readlink(string path)
    {
        var sz = __readlink_alias(path, _buf, (ulong)_buf.Length);
        do
        {
            if (sz == -1) throw new Win32Exception();
            if (sz < _buf.Length) break;
            _buf = new byte[_buf.Length * 2];
        } while (true);

        return new string(_buf.AsSpan(0, (int)sz).ToArray().Select(x => (char)x).ToArray());
    }

    // ReSharper disable UnusedMember.Global
    public readonly struct LStatData(
        ulong stDev,
        ulong stIno,
        uint stMode,
        bool stIsDir,
        bool stIsReg,
        bool stIsLnk,
        ulong stNlink,
        uint stUid,
        uint stGid,
        ulong stRdev,
        long stSize,
        DateTime stAtim,
        DateTime stMtim,
        DateTime stCtim,
        long stBlksize,
        long stBlocks)
    {
        public ulong StDev { get; } = stDev;
        public ulong StIno { get; } = stIno;
        public uint StMode { get; } = stMode;
        public bool StIsDir { get; } = stIsDir;
        public bool StIsReg { get; } = stIsReg;
        public bool StIsLnk { get; } = stIsLnk;
        public ulong StNlink { get; } = stNlink;
        public uint StUid { get; } = stUid;
        public uint StGid { get; } = stGid;
        public ulong StRdev { get; } = stRdev;
        public long StSize { get; } = stSize;
        public DateTime StAtim { get; } = stAtim;
        public DateTime StMtim { get; } = stMtim;
        public DateTime StCtim { get; } = stCtim;
        public long StBlksize { get; } = stBlksize;
        public long StBlocks { get; } = stBlocks;
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
        public readonly long stBlksize = new();
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
}
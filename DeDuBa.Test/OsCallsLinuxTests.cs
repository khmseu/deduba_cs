using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using OsCallsLinux;
using OsCallsCommon;
using static OsCallsCommon.ValXfer;

namespace DeDuBa.Test
{
    public class OsCallsLinuxTests
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr ShimFn([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

        private static string FindLibPath()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidateDebug = Path.Combine(baseDir, "OsCallsLinuxShim", "bin", "Debug", "net8.0", "libOsCallsLinuxShim.so");
            var candidateRelease = Path.Combine(baseDir, "OsCallsLinuxShim", "bin", "Release", "net8.0", "libOsCallsLinuxShim.so");
            if (File.Exists(candidateDebug)) return candidateDebug;
            if (File.Exists(candidateRelease)) return candidateRelease;
            // fallback: search the repo
            var dir = new DirectoryInfo(baseDir);
            for (var i = 0; i < 8 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "OsCallsLinuxShim", "bin", "Debug", "net8.0", "libOsCallsLinuxShim.so");
                if (File.Exists(candidate)) return candidate;
                candidate = Path.Combine(dir.FullName, "OsCallsLinuxShim", "bin", "Release", "net8.0", "libOsCallsLinuxShim.so");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new FileNotFoundException("libOsCallsLinuxShim.so not found in test environment");
        }

        [Fact]
        public void LinuxShimHasExpectedExports()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
            var lib = FindLibPath();
            var handle = NativeLibrary.Load(lib);
            try
            {
                Assert.True(NativeLibrary.TryGetExport(handle, "linux_lstat", out _), "linux_lstat must exist");
                Assert.True(NativeLibrary.TryGetExport(handle, "linux_readlink", out _), "linux_readlink must exist");
                Assert.True(NativeLibrary.TryGetExport(handle, "linux_canonicalize_file_name", out _), "linux_canonicalize_file_name must exist");
            }
            finally
            {
                NativeLibrary.Free(handle);
            }
        }

        [Fact]
        public unsafe void LinuxLstatAndWrapperProduceSameOutput()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "hello");
                var lib = FindLibPath();
                var handle = NativeLibrary.Load(lib);
                try
                {
                    Assert.True(NativeLibrary.TryGetExport(handle, "linux_lstat", out var linux_fptr), "linux_lstat export expected");
                    Assert.True(NativeLibrary.TryGetExport(handle, "lstat", out var raw_fptr), "lstat export expected");
                    var linux_del = Marshal.GetDelegateForFunctionPointer<ShimFn>(linux_fptr);
                    var raw_del = Marshal.GetDelegateForFunctionPointer<ShimFn>(raw_fptr);

                    var v1 = linux_del(tmp);
                    var v2 = raw_del(tmp);
                    var j1 = ToNode((ValueT*)v1, tmp, "linux_lstat");
                    var j2 = ToNode((ValueT*)v2, tmp, "lstat");
                    Assert.Equal(j1.ToJsonString(), j2.ToJsonString());
                }
                finally
                {
                    NativeLibrary.Free(handle);
                }
            }
            finally
            {
                File.Delete(tmp);
            }
        }
    }
}

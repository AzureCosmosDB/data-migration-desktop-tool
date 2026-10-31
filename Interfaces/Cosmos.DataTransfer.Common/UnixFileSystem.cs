using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Cosmos.DataTransfer.Common;

[UnsupportedOSPlatform("windows")]
internal static class UnixFileSystem
{
    private const int FileTypeMask = 0xF000;
    private const int RegularFile = 0x8000;

    internal static bool IsRegularFile(string path)
    {
        if (LStat(path, out var status) != 0)
            throw new IOException($"Could not inspect overwrite destination '{path}'.", new Win32Exception(Marshal.GetLastPInvokeError()));

        return (status.Mode & FileTypeMask) == RegularFile;
    }

    // Use the runtime's normalized ABI, not the platform/architecture-specific libc stat layout.
    // Contract: dotnet/runtime src/native/libs/System.Native/pal_io.h (FileStatus and SystemNative_LStat).
    [DllImport("System.Native", EntryPoint = "SystemNative_LStat", SetLastError = true)]
    private static extern int LStat([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out FileStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileStatus
    {
        public int Flags;
        public int Mode;
        public uint UserId;
        public uint GroupId;
        public long Size;
        public long AccessTime;
        public long AccessTimeNanoseconds;
        public long ModificationTime;
        public long ModificationTimeNanoseconds;
        public long ChangeTime;
        public long ChangeTimeNanoseconds;
        public long BirthTime;
        public long BirthTimeNanoseconds;
        public long Device;
        public long SpecialDevice;
        public long Inode;
        public uint UserFlags;
    }
}

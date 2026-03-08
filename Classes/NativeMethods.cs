using System.Runtime.InteropServices;

namespace DirectMusicConverter.Classes
{
    internal static class NativeMethods
    {
        [DllImport("kernel32", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadLibrary(string fileName);

        [DllImport("kernel32", EntryPoint = "GetProcAddress", CharSet = CharSet.Ansi, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr moduleHandle, string procName);

        [DllImport("kernel32", EntryPoint = "FreeLibrary", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr moduleHandle);
    }
}
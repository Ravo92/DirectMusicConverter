using System.Runtime.InteropServices;

namespace DirectMusicConverter.Interfaces
{
    [ComImport]
    [Guid("19E7C08C-0A44-4E6A-A116-595A7CD5DE8C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDirectMusicLoader8
    {
        [PreserveSig]
        int GetObject(IntPtr descriptor, ref Guid riid, out IntPtr objectPointer);

        [PreserveSig]
        int SetObject(IntPtr descriptor);

        [PreserveSig]
        int SetSearchDirectory(ref Guid classGuid, [MarshalAs(UnmanagedType.LPWStr)] string path, int clearCache);

        [PreserveSig]
        int ScanDirectory(ref Guid classGuid, [MarshalAs(UnmanagedType.LPWStr)] string fileExtension, [MarshalAs(UnmanagedType.LPWStr)] string scanFileName);

        [PreserveSig]
        int CacheObject(IntPtr objectPointer);

        [PreserveSig]
        int ReleaseObject(IntPtr objectPointer);

        [PreserveSig]
        int ClearCache(ref Guid classGuid);

        [PreserveSig]
        int EnableCache(ref Guid classGuid, int enable);

        [PreserveSig]
        int EnumObject(ref Guid classGuid, int index, IntPtr descriptor);

        [PreserveSig]
        int CollectGarbage();
    }
}
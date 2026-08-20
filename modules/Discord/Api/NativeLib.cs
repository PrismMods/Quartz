using System.Runtime.InteropServices;
using Quartz.Core;
namespace Quartz.Features.Discord;
public static class NativeLib {
    private const int RtldNow = 2;
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    [DllImport("kernel32", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr WindowsLoad(string path);
    [DllImport("kernel32", EntryPoint = "GetProcAddress", SetLastError = true)]
    private static extern IntPtr WindowsSymbol(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string name);
    [DllImport("kernel32", EntryPoint = "FreeLibrary", SetLastError = true)]
    private static extern bool WindowsFree(IntPtr handle);
    [DllImport("libdl", EntryPoint = "dlopen")]
    private static extern IntPtr DlOpen(string path, int flags);
    [DllImport("libdl", EntryPoint = "dlsym")]
    private static extern IntPtr DlSym(IntPtr handle, string name);
    [DllImport("libdl", EntryPoint = "dlclose")]
    private static extern int DlClose(IntPtr handle);
    [DllImport("libc", EntryPoint = "dlopen")]
    private static extern IntPtr LibcOpen(string path, int flags);
    [DllImport("libc", EntryPoint = "dlsym")]
    private static extern IntPtr LibcSym(IntPtr handle, string name);
    [DllImport("libc", EntryPoint = "dlclose")]
    private static extern int LibcClose(IntPtr handle);
    public static IntPtr Load(string path) {
        if(string.IsNullOrEmpty(path)) return IntPtr.Zero;
        if(IsWindows) {
            try {
                return WindowsLoad(path);
            } catch(Exception e) {
                Diag.Ignore(e);
                return IntPtr.Zero;
            }
        }
        try {
            return DlOpen(path, RtldNow);
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        try {
            return LibcOpen(path, RtldNow);
        } catch(Exception e) {
            Diag.Ignore(e);
            return IntPtr.Zero;
        }
    }
    public static IntPtr Symbol(IntPtr handle, string name) {
        if(handle == IntPtr.Zero || string.IsNullOrEmpty(name)) return IntPtr.Zero;
        if(IsWindows) {
            try {
                return WindowsSymbol(handle, name);
            } catch(Exception e) {
                Diag.Ignore(e);
                return IntPtr.Zero;
            }
        }
        try {
            return DlSym(handle, name);
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        try {
            return LibcSym(handle, name);
        } catch(Exception e) {
            Diag.Ignore(e);
            return IntPtr.Zero;
        }
    }
    public static T Bind<T>(IntPtr handle, string name) where T : class {
        IntPtr symbol = Symbol(handle, name);
        if(symbol == IntPtr.Zero) return null;
        try {
            return Marshal.GetDelegateForFunctionPointer(symbol, typeof(T)) as T;
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    public static void Free(IntPtr handle) {
        if(handle == IntPtr.Zero) return;
        try {
            if(IsWindows) WindowsFree(handle);
            else DlClose(handle);
        } catch(Exception e) {
            Diag.Ignore(e);
        }
    }
    public static string SystemLibrary() {
        if(IsWindows) return "kernel32.dll";
        if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "/usr/lib/libSystem.B.dylib";
        return "libc.so.6";
    }
    public static string SystemSymbol() => IsWindows ? "GetTickCount" : "malloc";
}

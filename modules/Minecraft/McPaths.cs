#nullable enable
using System.Runtime.InteropServices;
using Quartz.Core;
namespace Quartz.Features.Minecraft;
public readonly struct McEngineLocation(string executable, string workingDirectory) {
    public string Executable { get; } = executable;
    public string WorkingDirectory { get; } = workingDirectory;
}
public static class McPaths {
    public const string EngineVersion = "2.2.8";
    public const string Registry = "https://upm-pkgs.voltstro.dev";
    private const string ExecutableName = "UnityWebBrowser.Engine.Cef";
    public static string InstallRoot(string dataRoot) => Path.Combine(dataRoot, "Minecraft", "Engine");
    public static string VersionMarker(string dataRoot) => Path.Combine(InstallRoot(dataRoot), ".engine-version");
    // Without an explicit cache path the engine makes a fresh temp dir per launch, so
    // every visit to the tab re-downloads the whole page cold. This makes it persist.
    public static string CachePath(string dataRoot) => Path.Combine(dataRoot, "Minecraft", "Cache");
    public static bool IsInstalled(string dataRoot) {
        try {
            return File.Exists(VersionMarker(dataRoot))
                && File.ReadAllText(VersionMarker(dataRoot)).Trim() == PackageId() + "@" + EngineVersion
                && Locate(dataRoot) != null;
        } catch(Exception e) { Diag.Ignore(e); return false; }
    }
    public static string? PackageId() {
        bool arm = RuntimeInformation.OSArchitecture is Architecture.Arm64;
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return arm ? null : "dev.voltstro.unitywebbrowser.engine.cef.win.x64";
        if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return arm
            ? "dev.voltstro.unitywebbrowser.engine.cef.macos.arm64"
            : "dev.voltstro.unitywebbrowser.engine.cef.macos.x64";
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return arm ? null : "dev.voltstro.unitywebbrowser.engine.cef.linux.x64";
        return null;
    }
    public static string TarballUrl() {
        string? id = PackageId();
        return id == null ? string.Empty : $"{Registry}/{id}/-/{id}-{EngineVersion}.tgz";
    }
    private static string cachedRoot = string.Empty;
    private static McEngineLocation? cachedLocation;
    public static void InvalidateCache() {
        cachedRoot = string.Empty;
        cachedLocation = null;
    }
    public static McEngineLocation? Locate(string dataRoot) {
        string root = InstallRoot(dataRoot);
        // Walking the engine tree is a recursive scan of a few hundred files; the page
        // build and every OnEnable ask for it, so keep the answer.
        if(cachedRoot == root && cachedLocation != null && File.Exists(cachedLocation.Value.Executable))
            return cachedLocation;
        if(!Directory.Exists(root)) return null;
        try {
            // The launcher rule verified on macOS is that CefGlue resolves the CEF
            // framework at <cwd>/../Frameworks, so the working directory must be the
            // executable's own folder — which also holds on Windows and Linux, where
            // the loader looks beside the executable. Discovering the binary instead
            // of hard-coding a per-platform layout keeps this correct if the upstream
            // package rearranges itself.
            foreach(string candidate in Directory.GetFiles(root, ExecutableName, SearchOption.AllDirectories)) {
                string? dir = Path.GetDirectoryName(candidate);
                if(dir != null) return Remember(root, new McEngineLocation(candidate, dir));
            }
            foreach(string candidate in Directory.GetFiles(root, ExecutableName + ".exe", SearchOption.AllDirectories)) {
                string? dir = Path.GetDirectoryName(candidate);
                if(dir != null) return Remember(root, new McEngineLocation(candidate, dir));
            }
        } catch(Exception e) { Diag.Ignore(e); }
        return null;
    }
    private static McEngineLocation Remember(string root, McEngineLocation location) {
        cachedRoot = root;
        cachedLocation = location;
        return location;
    }
}

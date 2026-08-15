using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.UpdateEngine;
// Turns a downloaded install zip into Runtime/versions/<version>/. The zip is
// the ordinary release asset (Mods DLL + data files + runtime store), so this
// routes entries rather than extracting wholesale: the versioned runtime subtree
// lands as the new runtime, the maintainer-owned data files (Lang, bundled
// modules) are staged into .data-sync/ for the bootstrap to copy over the data
// root, and the rest (bootstrap DLL, state.json) is deliberately dropped — the
// frozen bootstrap only updates through the in-game installer.
internal static class PackageInstaller {
    private const long MaximumExtractedBytes = 512L * 1024 * 1024;
    public const string DataSyncDirName = ".data-sync";
    public const string RuntimeMarkerName = "runtime.json";
    public static string Install(string packagePath, string runtimeRoot, string version) {
        string versionsRoot = Path.Combine(runtimeRoot, "versions");
        string staging = Path.Combine(runtimeRoot, "extract-" + Guid.NewGuid().ToString("N"));
        string target = Path.Combine(versionsRoot, version);
        try {
            ExtractRuntime(packagePath, staging, version);
            ValidateRuntime(staging, version);
            Directory.CreateDirectory(versionsRoot);
            if(Directory.Exists(target)) Directory.Delete(target, true);
            Directory.Move(staging, target);
            return target;
        } finally {
            TryDeleteDirectory(staging);
        }
    }
    private static void ExtractRuntime(string packagePath, string destinationRoot, string version) {
        string runtimePrefix = EngineInfo.ZipRuntimeRel + "/versions/" + version + "/";
        string dataPrefix = EngineInfo.ZipDataRel + "/";
        string runtimeRelPrefix = EngineInfo.ZipRuntimeRel + "/";
        Directory.CreateDirectory(destinationRoot);
        string rootPrefix = EnsureTrailingSeparator(Path.GetFullPath(destinationRoot));
        using FileStream package = File.OpenRead(packagePath);
        using ZipArchive archive = new(package, ZipArchiveMode.Read);
        long extractedBytes = 0;
        bool sawRuntime = false;
        foreach(ZipArchiveEntry entry in archive.Entries) {
            string name = entry.FullName.Replace('\\', '/');
            string relative;
            if(name.StartsWith(runtimePrefix, StringComparison.Ordinal)) {
                relative = name.Substring(runtimePrefix.Length);
                sawRuntime = true;
            } else if(name.StartsWith(dataPrefix, StringComparison.Ordinal)
                && !name.StartsWith(runtimeRelPrefix, StringComparison.Ordinal)) {
                relative = DataSyncDirName + "/" + name.Substring(dataPrefix.Length);
            } else {
                continue;
            }
            if(relative.Length == 0) continue;
            extractedBytes = checked(extractedBytes + entry.Length);
            if(extractedBytes > MaximumExtractedBytes)
                throw new InvalidDataException("the extracted update package is too large");
            string destinationPath = Path.GetFullPath(Path.Combine(
                destinationRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if(!destinationPath.StartsWith(rootPrefix, StringComparison.Ordinal))
                throw new InvalidDataException("the update package contains an unsafe path");
            if(string.IsNullOrEmpty(entry.Name)) {
                Directory.CreateDirectory(destinationPath);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationRoot);
            using Stream source = entry.Open();
            using FileStream destination = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(destination);
            RestoreUnixPermissions(entry, destinationPath);
        }
        if(!sawRuntime)
            throw new InvalidDataException($"the update package has no runtime under {runtimePrefix}");
    }
    public static bool IsValidRuntime(string directory, string version) {
        try {
            ValidateRuntime(directory, version);
            return true;
        } catch(Exception e) {
            _ = e.Message;
            return false;
        }
    }
    public static void ValidateRuntime(string directory, string expectedVersion) {
        string payload = Path.Combine(directory, EngineInfo.PayloadFileName);
        string engine = Path.Combine(directory, EngineInfo.EngineFileName);
        string marker = Path.Combine(directory, RuntimeMarkerName);
        if(!File.Exists(payload) || !File.Exists(engine) || !File.Exists(marker))
            throw new InvalidDataException("the runtime is incomplete: " + directory);
        string stamped = JObject.Parse(File.ReadAllText(marker)).Value<string>("Version") ?? "";
        if(!SemVer.TryParse(stamped, out SemVer stampedVersion)
            || !SemVer.TryParse(expectedVersion, out SemVer expected)
            || stampedVersion.ToString() != expected.ToString())
            throw new InvalidDataException($"the runtime version marker '{stamped}' does not match '{expectedVersion}'");
    }
    public static void CleanupTemporaryArtifacts(string runtimeRoot) {
        if(!Directory.Exists(runtimeRoot)) return;
        foreach(string file in Directory.GetFiles(runtimeRoot, "download-*.zip")) TryDeleteFile(file);
        foreach(string directory in Directory.GetDirectories(runtimeRoot, "extract-*")) TryDeleteDirectory(directory);
    }
    private static string EnsureTrailingSeparator(string path) {
        return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
    private static void RestoreUnixPermissions(ZipArchiveEntry entry, string destinationPath) {
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        uint mode = (uint)(entry.ExternalAttributes >> 16) & 0x1FF;
        if(mode != 0 && Chmod(destinationPath, mode) != 0)
            throw new IOException("could not restore packaged file permissions: " + destinationPath);
    }
    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int Chmod(string path, uint mode);
    public static void TryDeleteFile(string path) {
        try {
            if(File.Exists(path)) File.Delete(path);
        } catch(Exception e) { _ = e.Message; }
    }
    public static void TryDeleteDirectory(string path) {
        try {
            if(Directory.Exists(path)) Directory.Delete(path, true);
        } catch(Exception e) { _ = e.Message; }
    }
}

#nullable enable
using System.Text;
using Quartz.Core;
namespace Quartz.IO;
public static class AtomicFile {
    internal const string BackupSuffix = ".quartz-atomic.bak";
    private static readonly object Gate = new();
    private static readonly StringComparison PathComparison = Path.DirectorySeparatorChar == '\\'
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
    private static readonly StringComparer PathComparer = Path.DirectorySeparatorChar == '\\'
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    public static void WriteAllText(string path, string contents) => WriteAllBytes(path, Encoding.UTF8.GetBytes(contents ?? string.Empty));
    public static void WriteAllBytes(string path, byte[] contents) {
        if(string.IsNullOrEmpty(path)) throw new ArgumentException("Destination path is required.", nameof(path));
        if(contents == null) throw new ArgumentNullException(nameof(contents));
        lock(Gate) {
            string fullPath = FullDestinationPath(path, nameof(path));
            string? directory = Path.GetDirectoryName(fullPath);
            if(!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            RecoverFileCore(fullPath);
            string tempPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            bool committed = false;
            try {
                using(FileStream stream = new(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    stream.Write(contents, 0, contents.Length);
                    stream.Flush(true);
                }
                if(File.Exists(fullPath)) {
                    try {
                        File.Replace(tempPath, fullPath, null);
                        committed = true;
                        return;
                    } catch(PlatformNotSupportedException e) { Diag.Ignore(e); } catch(IOException e) { Diag.Ignore(e); }
                    ReplaceWithBackupCore(tempPath, fullPath);
                    committed = true;
                    return;
                }
                File.Move(tempPath, fullPath);
                committed = true;
            } finally {
                if(!committed) try { File.Delete(tempPath); } catch(Exception e) { Diag.Ignore(e); }
            }
        }
    }
    internal static void ReplaceWithBackup(string tempPath, string fullPath) {
        if(string.IsNullOrEmpty(tempPath)) throw new ArgumentException("Temporary path is required.", nameof(tempPath));
        if(string.IsNullOrEmpty(fullPath)) throw new ArgumentException("Destination path is required.", nameof(fullPath));
        lock(Gate) {
            string destination = FullDestinationPath(fullPath, nameof(fullPath));
            RecoverFileCore(destination);
            ReplaceWithBackupCore(Path.GetFullPath(tempPath), destination);
        }
    }
    private static void ReplaceWithBackupCore(string tempPath, string fullPath) {
        string backupPath = BackupPath(fullPath);
        bool backedUp = false;
        bool installed = false;
        try {
            File.Move(fullPath, backupPath);
            backedUp = true;
            File.Move(tempPath, fullPath);
            installed = true;
        } catch {
            if(backedUp && !File.Exists(fullPath) && File.Exists(backupPath))
                File.Move(backupPath, fullPath);
            throw;
        } finally {
            if(installed && File.Exists(backupPath)) {
                try { File.Delete(backupPath); } catch(Exception e) { Diag.Ignore(e); }
            }
        }
    }
    internal static string BackupPath(string path) => Path.GetFullPath(path) + BackupSuffix;
    internal static void RecoverFile(string path) {
        if(string.IsNullOrEmpty(path)) throw new ArgumentException("Destination path is required.", nameof(path));
        lock(Gate) RecoverFileCore(FullDestinationPath(path, nameof(path)));
    }
    internal static void RecoverTree(string rootPath, params string[] excludedDirectories) {
        if(string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Recovery root is required.", nameof(rootPath));
        lock(Gate) {
            string root = Path.GetFullPath(rootPath);
            EnsureDirectoryIsSafe(root, "recovery root");
            HashSet<string> excluded = new(PathComparer);
            if(excludedDirectories != null) {
                string prefix = WithSeparator(root);
                foreach(string path in excludedDirectories) {
                    if(string.IsNullOrWhiteSpace(path)) continue;
                    string full = Path.GetFullPath(path);
                    if(full.StartsWith(prefix, PathComparison)) excluded.Add(full);
                }
            }
            RecoverDirectory(root, root, excluded);
        }
    }
    private static void RecoverDirectory(string root, string directory, HashSet<string> excluded) {
        EnsureSafeDirectoryPath(root, directory);
        string[] directories = TryEnumerate(() => Directory.GetDirectories(directory));
        string[] backups = TryEnumerate(
            () => Directory.GetFiles(directory, "*" + BackupSuffix, SearchOption.TopDirectoryOnly)
        );
        // Do not process names obtained through a directory swapped to a link.
        EnsureSafeDirectoryPath(root, directory);
        foreach(string entry in directories) {
            try {
                if(excluded.Contains(Path.GetFullPath(entry))) continue;
                if(!TryGetAttributes(entry, out FileAttributes attributes)) continue;
                if((attributes & FileAttributes.ReparsePoint) != 0) continue;
                if((attributes & FileAttributes.Directory) != 0) RecoverDirectory(root, entry, excluded);
            } catch(Exception e) when(IsExpectedRecoveryFailure(e)) {
                Diag.Warn(e, "Atomic recovery");
            }
        }
        foreach(string entry in backups) {
            try {
                if(!TryGetAttributes(entry, out FileAttributes attributes)) continue;
                if((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) continue;
                string name = Path.GetFileName(entry);
                if(name.Length <= BackupSuffix.Length
                    || !name.EndsWith(BackupSuffix, PathComparison)) continue;
                EnsureSafeDirectoryPath(root, Path.GetDirectoryName(entry)!);
                RecoverFileCore(entry.Substring(0, entry.Length - BackupSuffix.Length));
            } catch(Exception e) when(IsExpectedRecoveryFailure(e)) {
                Diag.Warn(e, "Atomic recovery");
            }
        }
    }
    private static void RecoverFileCore(string fullPath) {
        string backupPath = BackupPath(fullPath);
        bool backupExists = SafeRegularFileExists(backupPath, "atomic backup");
        if(!backupExists) return;
        bool destinationExists = SafeRegularFileExists(fullPath, "atomic destination");
        if(!destinationExists) {
            if(!SafeRegularFileExists(backupPath, "atomic backup")) return;
            destinationExists = SafeRegularFileExists(fullPath, "atomic destination");
            if(!destinationExists) {
                File.Move(backupPath, fullPath);
                return;
            }
        }
        // Destination creation is the commit point. A leftover backup means the
        // process stopped before cleanup, so keep the committed destination.
        if(!SafeRegularFileExists(fullPath, "atomic destination")) {
            if(SafeRegularFileExists(backupPath, "atomic backup")) File.Move(backupPath, fullPath);
            return;
        }
        if(!SafeRegularFileExists(backupPath, "atomic backup")) return;
        try { File.Delete(backupPath); }
        catch(Exception e) when(IsExpectedRecoveryFailure(e)) {
            // The destination is already committed. Keeping its old backup is
            // safe; a later startup/write will retry cleanup.
            Diag.Warn(e, "Atomic recovery");
        }
    }
    private static bool SafeRegularFileExists(string path, string label) {
        if(!TryGetAttributes(path, out FileAttributes attributes)) return false;
        if((attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"{label} is a symbolic link or reparse point: {path}");
        if((attributes & FileAttributes.Directory) != 0)
            throw new IOException($"{label} is a directory: {path}");
        return true;
    }
    private static void EnsureSafeDirectoryPath(string root, string directory) {
        string full = Path.GetFullPath(directory);
        string prefix = WithSeparator(root);
        if(!string.Equals(full, root, PathComparison) && !full.StartsWith(prefix, PathComparison))
            throw new IOException($"Atomic recovery path escapes its root: {directory}");
        EnsureDirectoryIsSafe(root, "recovery root");
        if(string.Equals(full, root, PathComparison)) return;
        string relative = full.Substring(prefix.Length);
        string current = root;
        foreach(string segment in relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries
        )) {
            current = Path.Combine(current, segment);
            EnsureDirectoryIsSafe(current, "recovery directory");
        }
    }
    private static void EnsureDirectoryIsSafe(string path, string label) {
        if(!TryGetAttributes(path, out FileAttributes attributes))
            throw new DirectoryNotFoundException($"Atomic {label} does not exist: {path}");
        if((attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Atomic {label} is a symbolic link or reparse point: {path}");
        if((attributes & FileAttributes.Directory) == 0)
            throw new IOException($"Atomic {label} is not a directory: {path}");
    }
    private static bool TryGetAttributes(string path, out FileAttributes attributes) {
        try {
            attributes = File.GetAttributes(path);
            return true;
        } catch(Exception expectedMissing) when(
            expectedMissing is FileNotFoundException or DirectoryNotFoundException
        ) {
            // A missing destination or backup is a normal recovery state.
            _ = expectedMissing;
            attributes = default;
            return false;
        }
    }
    private static bool IsExpectedRecoveryFailure(Exception e) =>
        e is IOException or UnauthorizedAccessException;
    private static string[] TryEnumerate(Func<string[]> enumerate) {
        try { return enumerate(); }
        catch(Exception e) when(IsExpectedRecoveryFailure(e)) {
            Diag.Warn(e, "Atomic recovery");
            return [];
        }
    }
    private static string FullDestinationPath(string path, string parameterName) {
        string fullPath = Path.GetFullPath(path);
        if(fullPath.EndsWith(BackupSuffix, PathComparison))
            throw new ArgumentException($"Destination paths may not use the reserved '{BackupSuffix}' suffix.", parameterName);
        return fullPath;
    }
    private static string WithSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
        || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
}

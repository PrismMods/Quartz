#nullable enable
using System.IO.Compression;
using Quartz.Core;
namespace Quartz.Update;
internal readonly struct StagedInstallFile {
    internal string Source { get; }
    internal string Destination { get; }
    internal StagedInstallFile(string source, string destination) {
        Source = source;
        Destination = destination;
    }
}
internal static class UpdateInstallTransaction {
    private const int MaxEntries = 4096;
    private const long MaxExtractedBytes = 1024L * 1024 * 1024;
    private static readonly StringComparison PathComparison = Path.DirectorySeparatorChar == '\\'
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
    internal static IReadOnlyList<StagedInstallFile> StageZip(
        string zipPath, string stageRoot, string installRoot
    ) {
        if(string.IsNullOrWhiteSpace(zipPath)) throw new ArgumentException("ZIP path is required.", nameof(zipPath));
        if(string.IsNullOrWhiteSpace(stageRoot)) throw new ArgumentException("Stage root is required.", nameof(stageRoot));
        if(string.IsNullOrWhiteSpace(installRoot)) throw new ArgumentException("Install root is required.", nameof(installRoot));
        string installFull = Path.GetFullPath(installRoot);
        string installPrefix = WithSeparator(installFull);
        string stageFull = Path.GetFullPath(stageRoot);
        string stagePrefix = WithSeparator(stageFull);
        EnsureRootDirectory(installFull, "install");
        RejectReparsePoint(stageFull, "stage root");
        if(Directory.Exists(stageFull) && Directory.GetFileSystemEntries(stageFull).Length != 0)
            throw new IOException("Update payload stage is not empty.");
        Directory.CreateDirectory(stageFull);
        EnsureRootDirectory(stageFull, "stage");
        List<StagedInstallFile> files = [];
        HashSet<string> destinations = new(PathComparer);
        long extractedBytes = 0;
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        if(archive.Entries.Count > MaxEntries) throw new InvalidDataException("Update ZIP has too many entries.");
        foreach(ZipArchiveEntry entry in archive.Entries) {
            string relative = NormalizeRelative(entry.FullName);
            bool directory = entry.FullName.EndsWith("/", StringComparison.Ordinal)
                || entry.FullName.EndsWith("\\", StringComparison.Ordinal);
            string destination = ContainedPath(installFull, installPrefix, relative, "install");
            EnsureNoReparsePoints(installFull, destination, "update destination");
            if(IsAtOrBelow(destination, stageFull, stagePrefix))
                throw new InvalidDataException($"Update ZIP entry targets its own staging area: {entry.FullName}");
            if(directory) continue;
            if(!destinations.Add(destination))
                throw new InvalidDataException($"Update ZIP contains duplicate destination: {entry.FullName}");
            try {
                extractedBytes = checked(extractedBytes + entry.Length);
            } catch(OverflowException e) {
                throw new InvalidDataException("Update ZIP expanded size is invalid.", e);
            }
            if(entry.Length < 0 || extractedBytes > MaxExtractedBytes)
                throw new InvalidDataException("Update ZIP expands beyond the safety limit.");
            string staged = ContainedPath(stageFull, stagePrefix, relative, "stage");
            string? parent = Path.GetDirectoryName(staged);
            if(string.IsNullOrEmpty(parent)) throw new InvalidDataException("Update ZIP entry has no parent directory.");
            CreateSafeDirectories(stageFull, parent, "stage");
            EnsureNoReparsePoints(stageFull, staged, "staged update path");
            using(Stream input = entry.Open())
            using(FileStream output = new(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                input.CopyTo(output);
                output.Flush(true);
                if(output.Length != entry.Length)
                    throw new InvalidDataException($"Update ZIP entry length changed while extracting: {entry.FullName}");
            }
            files.Add(new StagedInstallFile(staged, destination));
        }
        if(files.Count == 0) throw new InvalidDataException("Update ZIP contains no files.");
        return files;
    }
    internal static void Commit(
        IReadOnlyList<StagedInstallFile> files, string stageRoot, string installRoot,
        Action<int>? beforeInstall = null
    ) {
        if(files == null) throw new ArgumentNullException(nameof(files));
        if(files.Count == 0) throw new InvalidDataException("Update has no files to install.");
        if(string.IsNullOrWhiteSpace(stageRoot)) throw new ArgumentException("Stage root is required.", nameof(stageRoot));
        if(string.IsNullOrWhiteSpace(installRoot)) throw new ArgumentException("Install root is required.", nameof(installRoot));
        string stageFull = Path.GetFullPath(stageRoot);
        string installFull = Path.GetFullPath(installRoot);
        string stagePrefix = WithSeparator(stageFull);
        string installPrefix = WithSeparator(installFull);
        EnsureRootDirectory(stageFull, "stage");
        EnsureRootDirectory(installFull, "install");
        string transactionId = Guid.NewGuid().ToString("N");
        List<Record> records = new(files.Count);
        HashSet<string> destinations = new(PathComparer);
        for(int i = 0; i < files.Count; i++) {
            string source = ContainedAbsolutePath(stageFull, stagePrefix, files[i].Source, "stage");
            string destination = ContainedAbsolutePath(installFull, installPrefix, files[i].Destination, "install");
            EnsureNoReparsePoints(stageFull, source, "staged update path");
            EnsureNoReparsePoints(installFull, destination, "update destination");
            if(!File.Exists(source)) throw new FileNotFoundException("Staged update file is missing.", source);
            if(Directory.Exists(destination))
                throw new IOException($"Update destination is a directory: {destination}");
            if(!destinations.Add(destination))
                throw new InvalidDataException($"Update destination is duplicated: {destination}");
            string? parent = Path.GetDirectoryName(destination);
            if(string.IsNullOrEmpty(parent)) throw new IOException($"Update destination has no parent: {destination}");
            string backup = Path.Combine(parent, $".quartz-update-{transactionId}-{i}.bak");
            EnsureNoReparsePoints(installFull, backup, "update backup path");
            if(File.Exists(backup) || Directory.Exists(backup))
                throw new IOException($"Update backup path already exists: {backup}");
            records.Add(new Record(source, destination, backup, stageFull, installFull));
        }
        try {
            for(int i = 0; i < records.Count; i++) {
                beforeInstall?.Invoke(i);
                Record record = records[i];
                string parent = Path.GetDirectoryName(record.Destination)!;
                CreateSafeDirectories(record.InstallRoot, parent, "install");
                EnsureNoReparsePoints(record.StageRoot, record.Source, "staged update path");
                EnsureNoReparsePoints(record.InstallRoot, record.Destination, "update destination");
                EnsureNoReparsePoints(record.InstallRoot, record.Backup, "update backup path");
                if(File.Exists(record.Destination)) {
                    File.Move(record.Destination, record.Backup);
                    record.BackedUp = true;
                }
                EnsureNoReparsePoints(record.StageRoot, record.Source, "staged update path");
                EnsureNoReparsePoints(record.InstallRoot, record.Destination, "update destination");
                File.Move(record.Source, record.Destination);
                record.Installed = true;
            }
        } catch(Exception commitError) {
            List<Exception> rollbackErrors = RollBack(records);
            if(rollbackErrors.Count == 0) throw;
            rollbackErrors.Insert(0, commitError);
            throw new IOException(
                $"Update commit failed and rollback had {rollbackErrors.Count - 1} error(s). Backups were preserved.",
                new AggregateException(rollbackErrors)
            );
        }
        foreach(Record record in records) {
            if(!record.BackedUp || !File.Exists(record.Backup)) continue;
            try {
                EnsureNoReparsePoints(record.InstallRoot, record.Backup, "update backup path");
                File.Delete(record.Backup);
            } catch(Exception e) {
                Diag.Ignore(e);
            }
        }
    }
    private static List<Exception> RollBack(List<Record> records) {
        List<Exception> errors = [];
        for(int i = records.Count - 1; i >= 0; i--) {
            Record record = records[i];
            try {
                EnsureNoReparsePoints(record.InstallRoot, record.Destination, "update destination");
                EnsureNoReparsePoints(record.InstallRoot, record.Backup, "update backup path");
                if(record.Installed && File.Exists(record.Destination)) File.Delete(record.Destination);
                EnsureNoReparsePoints(record.InstallRoot, record.Destination, "update destination");
                EnsureNoReparsePoints(record.InstallRoot, record.Backup, "update backup path");
                if(record.BackedUp && File.Exists(record.Backup)) File.Move(record.Backup, record.Destination);
            } catch(Exception e) {
                errors.Add(e);
            }
        }
        return errors;
    }
    private static string NormalizeRelative(string value) {
        if(string.IsNullOrEmpty(value)) throw new InvalidDataException("Update ZIP has an empty entry name.");
        string relative = value
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        if(Path.IsPathRooted(relative)) throw new InvalidDataException($"Update ZIP entry is rooted: {value}");
        foreach(string segment in relative.Split(Path.DirectorySeparatorChar))
            if(segment is "." or "..")
                throw new InvalidDataException($"Update ZIP entry contains a relative segment: {value}");
        return relative;
    }
    private static string ContainedPath(string root, string prefix, string relative, string label) {
        string path;
        try {
            path = Path.GetFullPath(Path.Combine(root, relative));
        } catch(Exception e) {
            throw new InvalidDataException($"Update ZIP entry has an invalid {label} path: {relative}", e);
        }
        if(string.Equals(path, root, PathComparison)
            || !path.StartsWith(prefix, PathComparison))
            throw new InvalidDataException($"Update ZIP entry escapes the {label} root: {relative}");
        return path;
    }
    private static string ContainedAbsolutePath(
        string root, string prefix, string value, string label
    ) {
        string path;
        try {
            path = Path.GetFullPath(value);
        } catch(Exception e) {
            throw new InvalidDataException($"Update has an invalid {label} path: {value}", e);
        }
        if(string.Equals(path, root, PathComparison) || !path.StartsWith(prefix, PathComparison))
            throw new InvalidDataException($"Update path escapes the {label} root: {value}");
        return path;
    }
    private static void EnsureRootDirectory(string root, string label) {
        if(!TryGetAttributes(root, out FileAttributes attributes))
            throw new DirectoryNotFoundException($"Update {label} root does not exist: {root}");
        RejectReparsePoint(root, label + " root", attributes);
        if((attributes & FileAttributes.Directory) == 0)
            throw new IOException($"Update {label} root is not a directory: {root}");
    }
    private static void CreateSafeDirectories(string root, string directory, string label) {
        string full = Path.GetFullPath(directory);
        string prefix = WithSeparator(root);
        if(!string.Equals(full, root, PathComparison) && !full.StartsWith(prefix, PathComparison))
            throw new InvalidDataException($"Update directory escapes the {label} root: {directory}");
        EnsureRootDirectory(root, label);
        if(string.Equals(full, root, PathComparison)) return;
        string relative = full.Substring(prefix.Length);
        string current = root;
        foreach(string segment in relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries
        )) {
            EnsureExistingDirectory(current, label);
            current = Path.Combine(current, segment);
            if(!TryGetAttributes(current, out _))
                Directory.CreateDirectory(current);
            EnsureExistingDirectory(current, label);
        }
    }
    private static void EnsureNoReparsePoints(string root, string path, string label) {
        string full = Path.GetFullPath(path);
        string prefix = WithSeparator(root);
        if(!string.Equals(full, root, PathComparison) && !full.StartsWith(prefix, PathComparison))
            throw new InvalidDataException($"Update {label} escapes its root: {path}");
        EnsureRootDirectory(root, label);
        if(string.Equals(full, root, PathComparison)) return;
        string relative = full.Substring(prefix.Length);
        string current = root;
        string[] segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries
        );
        for(int i = 0; i < segments.Length; i++) {
            current = Path.Combine(current, segments[i]);
            if(!TryGetAttributes(current, out FileAttributes attributes)) return;
            RejectReparsePoint(current, label, attributes);
            if(i < segments.Length - 1 && (attributes & FileAttributes.Directory) == 0)
                throw new IOException($"Update {label} has a non-directory ancestor: {current}");
        }
    }
    private static void EnsureExistingDirectory(string path, string label) {
        if(!TryGetAttributes(path, out FileAttributes attributes))
            throw new DirectoryNotFoundException($"Update {label} directory disappeared: {path}");
        RejectReparsePoint(path, label, attributes);
        if((attributes & FileAttributes.Directory) == 0)
            throw new IOException($"Update {label} path is not a directory: {path}");
    }
    private static void RejectReparsePoint(string path, string label) {
        if(TryGetAttributes(path, out FileAttributes attributes))
            RejectReparsePoint(path, label, attributes);
    }
    private static void RejectReparsePoint(string path, string label, FileAttributes attributes) {
        if((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Update {label} contains a symbolic link or reparse point: {path}");
    }
    private static bool TryGetAttributes(string path, out FileAttributes attributes) {
        try {
            attributes = File.GetAttributes(path);
            return true;
        } catch(Exception expectedMissing) when(
            expectedMissing is FileNotFoundException or DirectoryNotFoundException
        ) {
            // Missing components are the normal false result of this probe.
            _ = expectedMissing;
            attributes = default;
            return false;
        }
    }
    private static string WithSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
        || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
    private static bool IsAtOrBelow(string path, string root, string prefix) =>
        string.Equals(path, root, PathComparison)
        || path.StartsWith(prefix, PathComparison);
    private static readonly StringComparer PathComparer = Path.DirectorySeparatorChar == '\\'
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private sealed class Record(
        string source, string destination, string backup, string stageRoot, string installRoot
    ) {
        internal readonly string Source = source;
        internal readonly string Destination = destination;
        internal readonly string Backup = backup;
        internal readonly string StageRoot = stageRoot;
        internal readonly string InstallRoot = installRoot;
        internal bool BackedUp;
        internal bool Installed;
    }
}

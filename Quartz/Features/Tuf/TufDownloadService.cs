using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Quartz.Core;
namespace Quartz.Features.Tuf;
public readonly struct TufInstallRoot(string path, bool linked) {
    public string Path { get; } = path;
    public bool Linked { get; } = linked;
}
public sealed class TufDownloadService : IDisposable {
    private const long SpaceCheckInterval = 32L * 1024 * 1024;
    private const string StageFolder = ".quartz-tmp";
    private readonly string levelsRoot;
    private readonly Func<TufInstallRoot> resolveRoot;
    private readonly HttpClient http;
    private readonly SemaphoreSlim oneAtATime = new(1, 1);
    private CancellationTokenSource active;
    private const string LayoutMarker = ".layout-v2";
    public TufDownloadService(string levelsRoot, Func<TufInstallRoot> resolveRoot = null) {
        this.levelsRoot = Path.GetFullPath(levelsRoot);
        this.resolveRoot = resolveRoot;
        Directory.CreateDirectory(this.levelsRoot);
        MigrateLayout();
        http = new HttpClient(new HttpClientHandler {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (HttpRequestMessage _, X509Certificate2 _, X509Chain _, SslPolicyErrors _) => true
        }) {
            Timeout = Timeout.InfiniteTimeSpan
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-TUF/1.0");
    }
    private void MigrateLayout() {
        string marker = Path.Combine(levelsRoot, LayoutMarker);
        if(File.Exists(marker)) return;
        try {
            foreach(string dir in Directory.GetDirectories(levelsRoot))
                try { Directory.Delete(dir, true); } catch { }
            foreach(string file in Directory.GetFiles(levelsRoot))
                try { File.Delete(file); } catch { }
            File.WriteAllText(marker, "2");
        } catch(Exception e) {
            MainCore.Log.Wrn("[TUF] could not migrate the level cache layout: " + e.Message);
        }
    }
    public TufInstallRoot ActiveRoot() {
        try {
            TufInstallRoot resolved = resolveRoot?.Invoke() ?? default;
            if(!string.IsNullOrEmpty(resolved.Path)) return new(Path.GetFullPath(resolved.Path), resolved.Linked);
        } catch { }
        return new(levelsRoot, false);
    }
    public bool TryGetCachedChart(int id, out string chart) => TryGetCachedChart(id, null, out chart);
    public bool TryGetCachedChart(int id, string folderOverride, out string chart) {
        chart = null;
        if(id <= 0) return false;
        try {
            string folder = folderOverride ?? LevelFolder(id);
            chart = TufArchive.SelectChart(folder);
            return chart != null && TufArchive.IsChartUnderRoot(chart, folder);
        } catch { return false; }
    }
    public string LevelFolder(int id) {
        TufInstallRoot root = ActiveRoot();
        return Path.Combine(root.Path, TufInstallPaths.LevelFolderName(id, root.Linked));
    }
    public IReadOnlyList<string> ListCachedCharts(int id) => ListCachedCharts(id, null);
    public IReadOnlyList<string> ListCachedCharts(int id, string folderOverride) {
        if(id <= 0) return Array.Empty<string>();
        try {
            string folder = folderOverride ?? LevelFolder(id);
            return TufArchive.ListCharts(folder)
                .Where(c => TufArchive.IsChartUnderRoot(c, folder)).ToList();
        } catch { return Array.Empty<string>(); }
    }
    public async Task<string> DownloadAsync(TufLevel level, Action<TufItemState, float> progress, CancellationToken token) {
        if(level == null || level.Id <= 0 || !TufNetworkPolicy.IsAllowedDownloadUri(level.DownloadUri))
            throw new InvalidDataException("Level has no safe download URL.");
        if(TryGetCachedChart(level.Id, out string cached)) return cached;
        TufInstallRoot target = ActiveRoot();
        string stage = Path.Combine(target.Path, StageFolder);
        string part = Path.Combine(stage, level.Id + ".part");
        string extracting = Path.Combine(stage, level.Id + ".extracting");
        string final = Path.Combine(target.Path, TufInstallPaths.LevelFolderName(level.Id, target.Linked));
        bool acquired = false;
        try {
            acquired = await oneAtATime.WaitAsync(0, token).ConfigureAwait(false);
            if(!acquired) throw new InvalidOperationException("Another TUF download is active.");
            active = CancellationTokenSource.CreateLinkedTokenSource(token);
            active.CancelAfter(TimeSpan.FromMinutes(10));
            Directory.CreateDirectory(stage);
            CleanupFile(part);
            CleanupDirectory(extracting);
            progress?.Invoke(TufItemState.Downloading, 0f);
            Uri finalUri = await DownloadToFileAsync(level.DownloadUri, part, target.Path, progress, active.Token)
                .ConfigureAwait(false);
            progress?.Invoke(TufItemState.Extracting, 1f);
            long declared = TufArchive.DeclaredSize(part);
            if(TufDiskSpace.IsKnownInsufficient(target.Path, declared, out long freeToExtract))
                throw new IOException(NoSpaceMessage(target.Path, declared, freeToExtract));
            int skipped = TufArchive.Extract(part, extracting);
            TufArchive.FlattenSingleRoot(extracting);
            if(skipped > 0)
                MainCore.Log.Wrn($"[TUF] level {level.Id}: skipped {skipped} archive entr{(skipped == 1 ? "y" : "ies")} "
                    + "that could not be decompressed or written (unsupported zip method or illegal filename).");
            string archiveStem = Uri.UnescapeDataString(Path.GetFileName(finalUri.AbsolutePath));
            string chart = TufArchive.SelectChart(extracting, archiveStem);
            if(chart == null) throw new InvalidDataException("Archive contains no playable .adofai chart.");
            string relativeChart = Path.GetRelativePath(extracting, chart);
            if(Directory.Exists(final)) Directory.Delete(final, true);
            Directory.Move(extracting, final);
            string installed = Path.GetFullPath(Path.Combine(final, relativeChart));
            if(!TufArchive.IsChartUnderRoot(installed, target.Path)) throw new InvalidDataException("Installed chart path is unsafe.");
            level.InstallFolder = final;
            return installed;
        } finally {
            if(acquired) {
                CleanupFile(part);
                CleanupDirectory(extracting);
                CleanupStage(stage);
                active?.Dispose();
                active = null;
                oneAtATime.Release();
            }
        }
    }
    public bool DeleteLevel(int id, string folder, IEnumerable<string> knownRoots) {
        if(id <= 0 || string.IsNullOrWhiteSpace(folder)) return false;
        List<string> roots = TrustedRoots(knownRoots);
        if(!TufInstallPaths.IsOwnedLevelFolder(folder, roots)) {
            MainCore.Log.Wrn($"[TUF] refused to delete '{folder}': not a level folder under a known library root.");
            return false;
        }
        if(!TufInstallPaths.IsLevelFolderName(Path.GetFileName(Path.GetFullPath(folder)), out int folderId)
            || folderId != id) {
            MainCore.Log.Wrn($"[TUF] refused to delete '{folder}': folder name does not match level {id}.");
            return false;
        }
        try {
            Directory.Delete(Path.GetFullPath(folder), true);
            MainCore.Log.Msg($"[TUF] deleted level {id} from {folder}");
            return true;
        } catch(Exception e) {
            MainCore.Log.Wrn($"[TUF] could not delete level {id}: {e.Message}");
            return false;
        }
    }
    public string MoveLevel(int id, string from, string toRoot, bool linked,
        IReadOnlyList<string> trustedRoots, CancellationToken token) {
        if(!TufInstallPaths.IsOwnedLevelFolder(from, trustedRoots))
            throw new InvalidDataException($"Level {id} is not in a known library root.");
        string source = Path.GetFullPath(from);
        string target = Path.Combine(Path.GetFullPath(toRoot), TufInstallPaths.LevelFolderName(id, linked));
        if(string.Equals(source, target, PathComparison)) return source;
        Directory.CreateDirectory(Path.GetFullPath(toRoot));
        if(Directory.Exists(target)) Directory.Delete(target, true);
        try {
            Directory.Move(source, target);
            return target;
        } catch(IOException) {
        }
        try {
            CopyDirectory(source, target, token);
        } catch {
            CleanupDirectory(target);
            throw;
        }
        try { Directory.Delete(source, true); } catch(Exception e) {
            MainCore.Log.Wrn($"[TUF] level {id} was copied to the new library but the old copy "
                + $"could not be removed ({e.Message}); delete '{source}' by hand to reclaim the space.");
        }
        return target;
    }
    private static void CopyDirectory(string from, string to, CancellationToken token) {
        Directory.CreateDirectory(to);
        foreach(string file in Directory.EnumerateFiles(from)) {
            token.ThrowIfCancellationRequested();
            if((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
        }
        foreach(string dir in Directory.EnumerateDirectories(from)) {
            token.ThrowIfCancellationRequested();
            if((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
            CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)), token);
        }
    }
    private List<string> TrustedRoots(IEnumerable<string> knownRoots) {
        List<string> roots = [levelsRoot, ActiveRoot().Path];
        if(knownRoots != null) roots.AddRange(knownRoots.Where(r => !string.IsNullOrWhiteSpace(r)));
        return roots;
    }
    private async Task<Uri> DownloadToFileAsync(Uri start, string path, string root,
        Action<TufItemState, float> progress, CancellationToken token) {
        Uri current = start;
        for(int redirects = 0; redirects <= 5; redirects++) {
            await TufNetworkPolicy.EnsurePublicHostAsync(current, token).ConfigureAwait(false);
            using HttpRequestMessage request = new(HttpMethod.Get, current);
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);
            if((int)response.StatusCode is >= 300 and < 400) {
                if(redirects == 5 || response.Headers.Location == null) throw new HttpRequestException("Too many TUF download redirects.");
                current = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location : new Uri(current, response.Headers.Location);
                continue;
            }
            response.EnsureSuccessStatusCode();
            long? length = response.Content.Headers.ContentLength;
            if(length is > 0 && TufDiskSpace.IsKnownInsufficient(root, length.Value, out long free))
                throw new IOException(NoSpaceMessage(root, length.Value, free));
            using Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
            byte[] buffer = new byte[65536];
            long total = 0;
            long nextSpaceCheck = SpaceCheckInterval;
            while(true) {
                int read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if(read == 0) break;
                total += read;
                if(length is > 0 && total > length.Value)
                    throw new InvalidDataException("Level download sent more data than it declared.");
                if(length is null or <= 0 && total >= nextSpaceCheck) {
                    nextSpaceCheck = total + SpaceCheckInterval;
                    if(TufDiskSpace.IsKnownInsufficient(root, 0, out long remaining))
                        throw new IOException(NoSpaceMessage(root, total, remaining));
                }
                await output.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                progress?.Invoke(TufItemState.Downloading,
                    length is > 0 ? Math.Min(1f, (float)total / length.Value) : -1f);
            }
            return current;
        }
        throw new HttpRequestException("TUF download redirect failed.");
    }
    private static string NoSpaceMessage(string root, long need, long free) => string.Format(
        MainCore.Tr.Get("TUF_NO_SPACE", "Not enough space on the drive holding {0}. This level needs {1} and only {2} is free."),
        root, TufDiskSpace.Describe(need), TufDiskSpace.Describe(free));
    public void Cancel() => active?.Cancel();
    public void Dispose() {
        Cancel();
        http.Dispose();
    }
    private static void CleanupFile(string path) { try { if(File.Exists(path)) File.Delete(path); } catch { } }
    private static void CleanupDirectory(string path) { try { if(Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
    private static void CleanupStage(string path) {
        try {
            if(Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path);
        } catch { }
    }
    private static StringComparison PathComparison =>
        Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Async;
using Quartz.Core;
using Quartz.IO;
namespace Quartz.Features.KeyViewer.Layout;
/// <summary>
/// Owns the KeyViewer layout on disk: load, debounced save, and DM Note import/export.
///
/// The layout lives in its own file rather than inside KeyViewer.json. A DM Note preset may
/// embed base64 font, image and sound blobs (embeddedLocalFonts/Images/Sounds) that run to
/// megabytes, which has no business in the settings file every other feature shares — and a
/// standalone file makes an export a copy of what is already on disk.
/// </summary>
internal static class KvStore {
    private const string JsonExtension = ".json";
    private const string LayoutFileName = "KeyViewerLayout.json";
    private const string CorruptPrefix = "KeyViewerLayout.corrupt-";
    private const int MaxCorruptBackups = 99;
    private const string DefaultExportName = "quartz-keyviewer-preset.json";
    private const string FilterName = "JSON Preset";
    /// <summary>
    /// DM Note presets embed base64 blobs and are legitimately large, so this is only a floor
    /// against reading something that was never a preset into memory whole.
    /// </summary>
    private const long MaxImportBytes = 64L * 1024 * 1024;
    private static readonly object sync = new();
    private static readonly object requestLock = new();
    private static KvDocument current;
    private static CancellationTokenSource saveCts;
    internal static KvDocument Current {
        get {
            lock(sync) {
                current ??= Read();
                return current;
            }
        }
    }
    internal static string LayoutPath => Path.Combine(MainCore.Paths.RootPath, LayoutFileName);
    internal static void Load() {
        lock(sync) current = Read();
    }
    internal static void Replace(KvDocument doc) {
        if(doc == null) return;
        lock(sync) current = doc;
        RequestSave();
    }
    /// <summary>
    /// Never throws. <see cref="Current"/> resolves lazily from the render path, so a failure
    /// here has to degrade to an empty layout rather than take its caller down.
    /// </summary>
    private static KvDocument Read() {
        string path;
        string json;
        try {
            path = LayoutPath;
            if(!File.Exists(path)) return KvDocument.Empty();
            json = File.ReadAllText(path);
        } catch(Exception e) {
            Err($"Failed to read the layout file: {e}");
            // Not quarantined: an unreadable file is usually a locked or transient one, and
            // the contents may be perfectly good.
            return KvDocument.Empty();
        }
        try {
            return KvDocument.Parse(json);
        } catch(Exception e) {
            Err($"Layout file is corrupt: {e}");
            Quarantine(path);
            return KvDocument.Empty();
        }
    }
    /// <summary>Renames the bad file aside so the next save cannot destroy it.</summary>
    private static void Quarantine(string path) {
        try {
            if(string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            string dir = Path.GetDirectoryName(path);
            if(string.IsNullOrEmpty(dir)) return;
            for(int n = 1; n <= MaxCorruptBackups; n++) {
                string backup = Path.Combine(dir, CorruptPrefix + n + JsonExtension);
                if(File.Exists(backup)) continue;
                File.Move(path, backup);
                Msg($"Preserved the corrupt layout as {backup}");
                return;
            }
            Err($"Could not preserve the corrupt layout: {MaxCorruptBackups} backups already exist.");
        } catch(Exception e) {
            Err($"Could not preserve the corrupt layout: {e}");
        }
    }
    internal static bool Save() {
        CancelPendingSave();
        return SaveCore();
    }
    // Writes are ordered by a sequence stamped at serialize time so a direct
    // Save() is never overwritten afterwards by an older debounced write that
    // was still in flight on the thread pool.
    private static readonly object writeLock = new();
    private static long saveSeq;
    private static long lastWrittenSeq;
    private static bool SaveCore() {
        if(!TrySerialize(out string json, out long seq)) return false;
        return WriteJson(json, seq);
    }
    // The document is mutated by the editor on the main thread, so serialize
    // stays on the caller's (main) thread; only the multi-MB file write +
    // fsync go off-thread.
    private static bool TrySerialize(out string json, out long seq) {
        try {
            lock(sync) json = Current.ToJson(true);
            seq = Interlocked.Increment(ref saveSeq);
            return true;
        } catch(Exception e) {
            Err($"Failed to serialize the layout: {e}");
            json = null;
            seq = 0;
            return false;
        }
    }
    private static bool WriteJson(string json, long seq) {
        lock(writeLock) {
            if(seq < lastWrittenSeq) return true; // superseded by a newer write
            try {
                AtomicFile.WriteAllText(LayoutPath, json);
                lastWrittenSeq = seq;
                return true;
            } catch(Exception e) {
                Err($"Failed to save the layout file: {e}");
                return false;
            }
        }
    }
    internal static void RequestSave(int delay = 500) {
        CancellationTokenSource request = new();
        CancellationTokenSource previous;
        lock(requestLock) {
            previous = saveCts;
            saveCts = request;
        }
        previous?.Cancel();
        _ = SaveAfterDelay(delay, request);
    }
    private static async Task SaveAfterDelay(int delay, CancellationTokenSource request) {
        CancellationToken token = request.Token;
        try {
            await Task.Delay(delay, token);
            if(token.IsCancellationRequested) {
                request.Dispose();
                return;
            }
            MainThread.Enqueue(() => {
                bool isCurrent;
                lock(requestLock) {
                    isCurrent = ReferenceEquals(saveCts, request);
                }
                if(!isCurrent || token.IsCancellationRequested) {
                    request.Dispose();
                    return;
                }
                // The layout serialize + fsync can run to megabytes with embedded preset
                // blobs; if the debounce comes due mid-run (pause → quick resume, die →
                // retry), re-arm instead of hitching a played frame.
                if(SaveGate.ShouldDefer) {
                    _ = SaveAfterDelay(delay, request);
                    return;
                }
                lock(requestLock) {
                    if(ReferenceEquals(saveCts, request)) saveCts = null;
                }
                // Serialize on the main thread (the document is live), then
                // push the fsync-bearing write to the thread pool so no frame
                // pays for disk latency.
                if(TrySerialize(out string json, out long seq))
                    _ = Task.Run(() => WriteJson(json, seq));
                request.Dispose();
            });
        } catch(OperationCanceledException) {
            ClearRequest(request);
        } catch(Exception e) {
            ClearRequest(request);
            Err($"Failed to request a layout save: {e}");
        }
    }
    private static void ClearRequest(CancellationTokenSource request) {
        lock(requestLock) {
            if(ReferenceEquals(saveCts, request)) saveCts = null;
        }
        request.Dispose();
    }
    internal static void CancelPendingSave() {
        CancellationTokenSource pending;
        lock(requestLock) {
            pending = saveCts;
            saveCts = null;
        }
        pending?.Cancel();
    }
    internal static bool ImportFromPath(string path, out string error) {
        error = null;
        if(string.IsNullOrWhiteSpace(path)) {
            error = "No file selected.";
            return false;
        }
        string json;
        try {
            long length = new FileInfo(path).Length;
            if(length > MaxImportBytes) {
                error = $"That file is {length / (1024 * 1024)} MB — too large to be a preset.";
                Msg(error);
                return false;
            }
            json = File.ReadAllText(path);
        } catch(Exception e) {
            error = "Import failed: " + e.Message;
            Msg(error);
            return false;
        }
        KvDocument doc;
        try {
            doc = KvDocument.Parse(json);
        } catch(FormatException e) {
            error = "Not a DM Note preset: " + e.Message;
            Msg(error);
            return false;
        } catch(Exception e) {
            error = "That file is not readable as a preset: " + e.Message;
            Msg(error);
            return false;
        }
        // Add the preset's tabs to the current layout rather than replacing it, so a multi-tab
        // setup survives importing one more preset. The first imported tab becomes the selection.
        string added = Current.MergeFrom(doc);
        if(added != null) Current.SelectedTab = added;
        ApplyEmbeddedCss(doc);
        Save();
        Msg("Imported layout from " + path);
        return true;
    }
    /// <summary>
    /// Move the imported preset's own custom CSS into the CSS engine's state, so a CSS-styled
    /// DM Note preset renders as it does in DM Note rather than as bare boxes.
    ///
    /// Only when the user has no CSS of their own: import now ADDS tabs rather than replacing the
    /// document, and the CSS is global, so overwriting it would restyle every existing tab from a
    /// preset that was only meant to add one. A user starting empty still gets the preset's look.
    /// </summary>
    private static void ApplyEmbeddedCss(KvDocument doc) {
        KeyViewerSettings conf = KeyViewerOverlay.Conf;
        if(conf == null) return;
        if(!string.IsNullOrWhiteSpace(conf.DmCssText)) return;
        (bool enabled, string content) = doc.EmbeddedCss();
        if(string.IsNullOrWhiteSpace(content)) return;
        conf.DmCssText = content;
        conf.DmCssEnabled = enabled;
        conf.DmCssPath = "";
        KeyViewerOverlay.Save();
    }
    internal static bool ExportToPath(string path, out string error, bool includeCounts = true) {
        error = null;
        if(string.IsNullOrWhiteSpace(path)) {
            error = "No destination selected.";
            return false;
        }
        try {
            string json = Current.ToJson(true);
            if(!includeCounts) json = StripCounts(json);
            AtomicFile.WriteAllText(path, json);
            Msg("Exported layout to " + path);
            return true;
        } catch(Exception e) {
            error = "Export failed: " + e.Message;
            Msg(error);
            return false;
        }
    }
    /// <summary>
    /// Import through the native picker, the way TUF's folder picker does. On an exclusive-fullscreen
    /// macOS game the dialog opens on its own Space — the same behaviour every other picker in the
    /// mod has — so the library folder (see <see cref="LibraryFiles"/>) stays as the reliable path.
    /// </summary>
    internal static bool Import(out string error) {
        error = null;
        string picked;
        try {
            picked = UnityFileDialog.FileBrowser.PickFile(
                "", FilterName, ["json"], "Select a DM Note preset or Quartz layout");
        } catch(Exception e) {
            error = "Picker failed: " + e.Message;
            Msg(error);
            return false;
        }
        return string.IsNullOrEmpty(picked) || ImportFromPath(picked, out error);
    }
    internal static bool Export(out string error, bool includeCounts = true) {
        error = null;
        string picked;
        try {
            Directory.CreateDirectory(LibraryDir);
            picked = UnityFileDialog.FileBrowser.SaveFile(
                LibraryDir, DefaultExportName, FilterName, ["json"], "Export KeyViewer layout");
        } catch(Exception e) {
            error = "Picker failed: " + e.Message;
            Msg(error);
            return false;
        }
        return string.IsNullOrEmpty(picked) || ExportToPath(EnsureJsonExtension(picked), out error, includeCounts);
    }
    /// <summary>The dialog does not always append the filter extension, and DM Note lists only .json.</summary>
    private static string EnsureJsonExtension(string path) =>
        string.IsNullOrEmpty(Path.GetExtension(path)) ? path + JsonExtension : path;
    /// <summary>Open the presets folder in the OS file browser. No Space switch — it launches Finder.</summary>
    internal static void RevealLibrary() {
        try {
            Directory.CreateDirectory(LibraryDir);
            UnityFileDialog.FileBrowser.Reveal(LibraryDir);
        } catch(Exception e) {
            Msg("[KeyViewer] Reveal failed: " + e.Message);
        }
    }
    /// <summary>
    /// Zero every element's press counter in a serialized layout.
    ///
    /// DM Note persists live per-key counts into presets, so a faithful export ships the
    /// author's key counts to anyone they send the file to. Operates on a re-parsed copy so
    /// the in-memory document — and the user's own counts — are untouched. `count` stays
    /// present because DM Note's deserializer requires it on every position object.
    /// </summary>
    private static string StripCounts(string json) {
        JObject root = JObject.Parse(json);
        foreach(string table in new[] { "keyPositions", "statPositions", "graphPositions", "knobPositions" }) {
            if(root[table] is not JObject byTab) continue;
            foreach(JProperty tab in byTab.Properties()) {
                if(tab.Value is not JArray arr) continue;
                foreach(JToken entry in arr) {
                    JObject target = entry["position"] as JObject ?? entry as JObject;
                    if(target?["count"] != null) target["count"] = 0;
                }
            }
        }
        return root.ToString(Formatting.Indented);
    }
    /// <summary>
    /// A preset folder inside the mod's own data, NOT an OS file dialog.
    ///
    /// The native dialog (UnityFileDialog) opens on its own macOS Space, so from an exclusive-
    /// fullscreen game pressing Import/Export flips the user to another desktop and strands the
    /// dialog there — it is unusable. Import and export therefore work against a fixed, in-game
    /// browsable folder instead. Users drop a preset in here to import it and find their exports
    /// here to hand to DM Note.
    /// </summary>
    internal static string LibraryDir => Path.Combine(MainCore.Paths.RootPath, "Presets");
    /// <summary>The folder to name to a user with no presets yet, created so it is there to open.</summary>
    internal static string LibraryHint {
        get {
            try {
                Directory.CreateDirectory(LibraryDir);
            } catch { }
            return LibraryDir;
        }
    }
    private static string DownloadsDir {
        get {
            try {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return string.IsNullOrEmpty(home) ? null : Path.Combine(home, "Downloads");
            } catch {
                return null;
            }
        }
    }
    /// <summary>
    /// Every .json in the library folder and the user's Downloads, newest first, as (path, name).
    /// Downloads is included because that is where a preset shared from DM Note or a browser lands,
    /// so it can be imported without the user first moving it anywhere.
    /// </summary>
    internal static IReadOnlyList<(string Path, string Name)> LibraryFiles() {
        List<(string Path, string Name)> found = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        void Scan(string dir, string tag) {
            if(string.IsNullOrEmpty(dir)) return;
            try {
                if(!Directory.Exists(dir)) return;
                foreach(string file in Directory.EnumerateFiles(dir, "*" + JsonExtension)) {
                    // The live layout is not a preset the user would re-import over itself.
                    if(string.Equals(Path.GetFileName(file), LayoutFileName, StringComparison.OrdinalIgnoreCase)) continue;
                    if(!seen.Add(Path.GetFullPath(file))) continue;
                    found.Add((file, tag + Path.GetFileName(file)));
                }
            } catch(Exception e) {
                Msg("[KeyViewer] Could not scan " + dir + ": " + e.Message);
            }
        }
        Scan(LibraryDir, "");
        Scan(DownloadsDir, "Downloads/");
        found.Sort((a, b) => LastWrite(b.Path).CompareTo(LastWrite(a.Path)));
        return found;
    }
    private static DateTime LastWrite(string path) {
        try {
            return File.GetLastWriteTimeUtc(path);
        } catch {
            return DateTime.MinValue;
        }
    }
    /// <summary>
    /// Write the layout into the library folder under a fresh, non-clobbering name, reporting the
    /// path so the UI can point the user at it. Replaces the SaveFile dialog.
    /// </summary>
    internal static bool ExportToLibrary(bool includeCounts, out string savedPath, out string error) {
        savedPath = null;
        error = null;
        try {
            Directory.CreateDirectory(LibraryDir);
            savedPath = UniqueExportPath();
        } catch(Exception e) {
            error = "Export failed: " + e.Message;
            Msg(error);
            return false;
        }
        return ExportToPath(savedPath, out error, includeCounts);
    }
    private static string UniqueExportPath() {
        string baseName = Path.GetFileNameWithoutExtension(DefaultExportName);
        string candidate = Path.Combine(LibraryDir, baseName + JsonExtension);
        for(int n = 2; File.Exists(candidate) && n < 10000; n++)
            candidate = Path.Combine(LibraryDir, baseName + "-" + n + JsonExtension);
        return candidate;
    }
    // MainCore.Log resolves through the runtime, which is absent before init and during
    // teardown; Read must hold its never-throw guarantee even then.
    private static void Msg(string message) {
        try {
            MainCore.Log.Msg("[KeyViewer] " + message);
        } catch {
        }
    }
    private static void Err(string message) {
        try {
            MainCore.Log.Err("[KeyViewer] " + message);
        } catch {
        }
    }
}

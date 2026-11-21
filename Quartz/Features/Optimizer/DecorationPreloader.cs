using System.Collections;
using System.Threading.Tasks;
using ADOFAI;
using Quartz.Compat.Game;
using Quartz.Core;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;
namespace Quartz.Features.Optimizer;
// Vanilla builds every decoration sprite inside scnGame.UpdateDecorationObjects: one
// file read + Texture2D.LoadImage per image, all on the main thread, back to back. On
// a decoration-heavy level that is the whole load hitch.
//
// This decodes them ahead of time instead. Before the game parses the level, the
// image names are scanned out of the file on a worker thread, then every image goes
// through UnityWebRequestTexture, whose decode runs on Unity's worker threads, many
// in flight at once. The finished textures wait in `pending` until scnGame.LoadLevel
// succeeds for that same path, then are put into imgHolder exactly the way
// TextureManager.GetOrAddSprite would have stored them. The vanilla loop that runs
// next finds a cache hit for each one and skips its synchronous load.
//
// Anything that fails here (an unsupported format, an odd path) is simply left out,
// and vanilla loads it the old way, so the worst case is the old speed.
public static class DecorationPreloader {
    private sealed class Loaded {
        public Texture2D Texture;
        public DateTime LastWrite;
    }
    private sealed class Batch {
        public string LevelPath;
        public readonly Dictionary<string, Loaded> Textures = new(StringComparer.Ordinal);
        public int CompletedFrame = -1;
        public scnGame Owner;
    }
    // Unclaimed results are dropped after this many frames: the load they were meant
    // for was cancelled (scene change, failed open, a gate like AprilFools' quiz).
    private const int StaleFrames = 600;
    private static Batch pending;
    private static int runId;
    // Requests of the current run, kept here so a run whose host coroutine was
    // stopped (scene change) can still be aborted from Tick/DiscardPending.
    private static readonly List<(UnityWebRequest req, int job)> inFlight = [];
    private static int lastRunFrame = -1;
    public static bool Active => Optimizer.ParallelDecorationLoadingActive;
    internal static bool ShouldPreload(string levelPath) {
        if(!Active || string.IsNullOrEmpty(levelPath)) return false;
        if(GCS.internalLevelName != null || GameApi.IsBundleLevel()) return false;
        try {
            if(!string.Equals(Path.GetExtension(levelPath), ".adofai", StringComparison.OrdinalIgnoreCase)) return false;
            return File.Exists(levelPath);
        } catch(Exception e) {
            Diag.Ignore(e);
            return false;
        }
    }
    // Coroutine: yield it from the host that is about to load `levelPath`.
    // No "already cached" filter here: in the editor ReloadAssets evicts every entry the
    // previous level left before the decoration pass, so a hit now is gone by then.
    public static IEnumerator Run(string levelPath) {
        DiscardPending();
        int id = ++runId;
        string dir;
        try {
            levelPath = Path.GetFullPath(levelPath);
            dir = Path.GetDirectoryName(levelPath);
        } catch(Exception e) {
            Diag.Warn(e, "Optimizer/DecoPreload path");
            yield break;
        }
        Task<List<string>> scan = Task.Run(() => DecorationImageScanner.Scan(File.ReadAllText(levelPath)));
        while(!scan.IsCompleted) yield return null;
        if(scan.IsFaulted) {
            Diag.Warn(scan.Exception?.GetBaseException(), "Optimizer/DecoPreload scan");
            yield break;
        }
        List<(string key, string file, DateTime lastWrite)> jobs = [];
        foreach(string key in scan.Result) {
            if(SkipKey(key)) continue;
            string file;
            DateTime lastWrite;
            try {
                file = Path.Combine(dir, key);
                if(!File.Exists(file)) continue;
                lastWrite = new FileInfo(file).LastWriteTimeUtc;
            } catch(Exception e) {
                Diag.Ignore(e);
                continue;
            }
            jobs.Add((key, file, lastWrite));
        }
        if(jobs.Count == 0) yield break;
        Batch batch = new() { LevelPath = levelPath };
        pending = batch;
        bool compress = Optimizer.Conf != null && Optimizer.Conf.LossyTextureCompression;
        int maxInFlight = Mathf.Clamp(SystemInfo.processorCount, 2, 16);
        float started = Time.realtimeSinceStartup;
        DecorationLoadProgress.Begin(jobs.Count);
        AbortInFlight();
        int next = 0;
        int done = 0;
        try {
            while(done < jobs.Count) {
                if(id != runId || pending != batch) yield break;
                lastRunFrame = Time.frameCount;
                for(int i = inFlight.Count - 1; i >= 0; i--) {
                    (UnityWebRequest req, int job) = inFlight[i];
                    if(!req.isDone) continue;
                    inFlight.RemoveAt(i);
                    done++;
                    Texture2D tex = Finish(req, jobs[job].file, compress);
                    if(tex != null) batch.Textures[jobs[job].key] = new Loaded { Texture = tex, LastWrite = jobs[job].lastWrite };
                }
                while(next < jobs.Count && inFlight.Count < maxInFlight) {
                    UnityWebRequest req = Request(jobs[next].file, compress);
                    if(req == null) done++;
                    else inFlight.Add((req, next));
                    next++;
                }
                DecorationLoadProgress.Report(done, jobs.Count);
                if(done < jobs.Count) yield return null;
            }
        } finally {
            if(id == runId) {
                AbortInFlight();
                DecorationLoadProgress.End();
            }
        }
        batch.CompletedFrame = Time.frameCount;
        MainCore.Log.Msg($"[Optimizer] preloaded {batch.Textures.Count}/{jobs.Count} decoration images in "
            + $"{(Time.realtimeSinceStartup - started) * 1000f:0} ms ({maxInFlight} in flight)");
    }
    private static bool SkipKey(string key) {
        try {
            return ADOBase.IsNotAMikoSkipMandatorySprite(key);
        } catch(Exception e) {
            Diag.Ignore(e);
            return false;
        }
    }
    private static bool AlreadyCached(TextureManager holder, string key, DateTime lastWrite) {
        if(holder?.customSprites == null) return false;
        return holder.customSprites.TryGetValue(key, out TextureManager.CustomSprite existing)
            && existing != null && existing.texture != null
            && DateTime.Compare(existing.fileLastModified, lastWrite) == 0;
    }
    private static UnityWebRequest Request(string file, bool readable) {
        try {
            // Match TextureManager.LoadTexture: no mip chain, sRGB. Stay readable only
            // when Lossy Texture Compression needs the pixels to compress.
            DownloadedTextureParams p = new() {
                flags = readable ? DownloadedTextureFlags.Readable : DownloadedTextureFlags.None,
                mipmapCount = -1,
            };
            UnityWebRequest req = UnityWebRequestTexture.GetTexture(DecorationImageScanner.ToFileUri(file), p);
            req.SendWebRequest();
            return req;
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    private static Texture2D Finish(UnityWebRequest req, string file, bool compress) {
        try {
            if(req.result != UnityWebRequest.Result.Success) return null;
            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            if(tex == null) return null;
            if(tex.width <= 8 && tex.height <= 8 && new FileInfo(file).Length > 1024) {
                // Unity hands back a tiny placeholder for data it couldn't decode;
                // let vanilla load it and report the error itself.
                Object.Destroy(tex);
                return null;
            }
            // Same finishing steps as vanilla / OptimizerPatches.LoadTextureCompressPatch.
            if(compress) {
                tex.name = file;
                if(tex.width % 4 == 0 && tex.height % 4 == 0) tex.Compress(false);
                if(tex.isReadable) tex.Apply(false, true);
            } else {
                tex.name = Path.GetFileName(file);
            }
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        } finally {
            req.Dispose();
        }
    }
    // scnGame.LoadLevel postfix: bind the batch to the level that just loaded, or drop
    // it when that load was for another path or failed. The textures are not handed
    // over yet: ReloadAssets runs between here and the decoration pass, and in the
    // editor it marks the whole cache unused and unloads it, which would throw away
    // anything stored now.
    internal static void Bind(scnGame level, string levelPath, bool loaded) {
        Batch batch = pending;
        if(batch == null) return;
        if(!loaded || level == null || !SamePath(batch.LevelPath, levelPath)) {
            if(batch.CompletedFrame >= 0) DiscardPending();
            return;
        }
        if(batch.CompletedFrame < 0) return;
        batch.Owner = level;
    }
    // scnGame.UpdateDecorationObjects prefix: the vanilla loop is about to call
    // GetOrAddSprite for every decoration, so fill the cache now.
    internal static void Claim(scnGame level) {
        Batch batch = pending;
        if(batch == null || level == null || !ReferenceEquals(batch.Owner, level)) return;
        pending = null;
        TextureManager holder = level.imgHolder;
        if(holder?.customSprites == null) {
            Destroy(batch);
            return;
        }
        bool fromBundle = GameApi.IsBundleLevel();
        bool isInternal = GCS.internalLevelName != null;
        int added = 0;
        foreach(KeyValuePair<string, Loaded> kv in batch.Textures) {
            try {
                if(AlreadyCached(holder, kv.Key, kv.Value.LastWrite)) {
                    Object.Destroy(kv.Value.Texture);
                    continue;
                }
                if(holder.customSprites.ContainsKey(kv.Key)) holder.UnloadSprite(kv.Key);
                holder.customSprites[kv.Key] = new TextureManager.CustomSprite(
                    kv.Value.Texture, kv.Value.LastWrite, isInternal, fromBundle) { inUse = true };
                added++;
            } catch(Exception e) {
                Diag.Warn(e, "Optimizer/DecoPreload claim");
                if(kv.Value.Texture != null) Object.Destroy(kv.Value.Texture);
            }
        }
        batch.Textures.Clear();
        if(Diag.Verbose) MainCore.Log.Msg($"[Optimizer] handed {added} preloaded decoration images to the level");
    }
    internal static void Tick() {
        Batch batch = pending;
        if(batch == null) return;
        int now = Time.frameCount;
        bool orphaned = batch.CompletedFrame < 0 && lastRunFrame >= 0 && now - lastRunFrame > 30;
        bool stale = batch.CompletedFrame >= 0 && now - batch.CompletedFrame > StaleFrames;
        if(orphaned || stale) DiscardPending();
    }
    public static void DiscardPending() {
        Batch batch = pending;
        pending = null;
        runId++;
        AbortInFlight();
        DecorationLoadProgress.End();
        if(batch != null) Destroy(batch);
    }
    private static void Destroy(Batch batch) {
        foreach(Loaded l in batch.Textures.Values) {
            try { if(l.Texture != null) Object.Destroy(l.Texture); } catch(Exception e) { Diag.Ignore(e); }
        }
        batch.Textures.Clear();
    }
    private static void AbortInFlight() {
        foreach((UnityWebRequest req, _) in inFlight) {
            try { req.Abort(); req.Dispose(); } catch(Exception e) { Diag.Ignore(e); }
        }
        inFlight.Clear();
    }
    private static bool SamePath(string a, string b) {
        if(a == null || b == null) return false;
        try {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b),
                Application.platform == RuntimePlatform.LinuxPlayer ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        } catch(Exception e) {
            Diag.Ignore(e);
            return false;
        }
    }
}

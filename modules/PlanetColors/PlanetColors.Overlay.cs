using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.PlanetColors;
public static partial class PlanetColors {
    private const string OverlayObjectName = "Quartz_PlanetOverlay";
    private sealed class OverlayEntry {
        public SpriteRenderer Overlay;
        public SpriteRenderer Source;
    }
    private static readonly Dictionary<int, OverlayEntry> overlayMap = [];
    private static readonly Dictionary<string, Sprite> overlaySprites = new(StringComparer.Ordinal);
    private static readonly List<int> overlayPurgeBuffer = [];
    public static bool OverlayEnabled => ShouldChange && Conf.EnableOverlay;
    public static bool OverlayActive => OverlayEnabled && !IsEditingLevel;
    private static int overlayVisibilityFrame = -1;
    private static bool overlayVisibilityCache;
    private static bool OverlayVisibleThisFrame {
        get {
            int frame = Time.frameCount;
            if(overlayVisibilityFrame != frame) {
                overlayVisibilityFrame = frame;
                overlayVisibilityCache = OverlayActive;
            }
            return overlayVisibilityCache;
        }
    }
    private static bool IsEditingLevel {
        get {
            try { return scnEditor.instance is { playMode: false }; }
            catch { return false; }
        }
    }
    private static void ReconcileOverlayVisibility(PlanetRenderer renderer) {
        if(renderer == null) return;
        if(!overlayMap.TryGetValue(renderer.GetInstanceID(), out OverlayEntry entry)) return;
        if(entry == null) return;
        SpriteRenderer overlay = entry.Overlay;
        if(overlay == null) return;
        try {
            if(entry.Source == null) entry.Source = PlanetSpriteRenderer(renderer);
            bool visible = OverlayVisibleThisFrame && overlay.sprite != null && PlanetIsShowing(entry);
            if(overlay.gameObject.activeSelf != visible) overlay.gameObject.SetActive(visible);
            if(visible) SyncOverlayAlpha(overlay, entry);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static bool PlanetIsShowing(OverlayEntry entry) {
        SpriteRenderer source = entry?.Source;
        if(source == null) return true;
        return source.enabled && source.gameObject.activeInHierarchy;
    }
    private static void SyncOverlayAlpha(SpriteRenderer overlay, OverlayEntry entry) {
        SpriteRenderer source = entry?.Source;
        if(source == null) return;
        float alpha = source.color.a;
        Color current = overlay.color;
        if(current.a == alpha) return;
        current.a = alpha;
        overlay.color = current;
    }
    public static void ClearOverlayCaches() {
        overlayMap.Clear();
        DisposeOverlaySprites();
    }
    public static void ReloadOverlayImages() {
        DisposeOverlaySprites();
        Refresh();
    }
    private static void DisposeOverlaySprites() {
        foreach(Sprite sprite in overlaySprites.Values) {
            if(sprite == null) continue;
            Texture2D tex = null;
            try { tex = sprite.texture; } catch(Exception e) { Diag.Ignore(e); }
            try { UnityEngine.Object.Destroy(sprite); } catch(Exception e) { Diag.Ignore(e); }
            if(tex != null) {
                try { UnityEngine.Object.Destroy(tex); } catch(Exception e) { Diag.Ignore(e); }
            }
        }
        overlaySprites.Clear();
    }
    private static void ApplyOverlayToPlanet(PlanetRenderer renderer, int slot) {
        if(renderer == null) return;
        if(!OverlayEnabled) {
            HideOverlay(renderer);
            return;
        }
        Sprite sprite = LoadOverlaySprite(Conf.GetOverlayPath(slot));
        if(sprite == null) {
            HideOverlay(renderer);
            return;
        }
        OverlayEntry entry = GetOrCreateOverlay(renderer);
        if(entry == null) return;
        SpriteRenderer overlay = entry.Overlay;
        if(overlay == null) return;
        try {
            if(overlay.sprite != sprite) overlay.sprite = sprite;
            float scale = Conf.GetOverlayScale(slot);
            Transform t = overlay.transform;
            if(t.localScale.x != scale) t.localScale = Vector3.one * scale;
            SpriteRenderer source = entry.Source;
            if(source != null) {
                overlay.sortingLayerID = source.sortingLayerID;
                overlay.sortingOrder = source.sortingOrder + 10;
            }
            overlay.color = Color.white;
            SyncOverlayAlpha(overlay, entry);
            bool visible = OverlayActive && PlanetIsShowing(entry);
            if(overlay.gameObject.activeSelf != visible) overlay.gameObject.SetActive(visible);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void HideOverlay(PlanetRenderer renderer) {
        if(renderer == null) return;
        int id = renderer.GetInstanceID();
        if(!overlayMap.TryGetValue(id, out OverlayEntry entry)) return;
        SpriteRenderer overlay = entry?.Overlay;
        if(overlay == null) {
            overlayMap.Remove(id);
            return;
        }
        try {
            if(overlay.gameObject.activeSelf) overlay.gameObject.SetActive(false);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    public static void DisableAllOverlays() {
        overlayPurgeBuffer.Clear();
        foreach(KeyValuePair<int, OverlayEntry> pair in overlayMap) {
            SpriteRenderer overlay = pair.Value?.Overlay;
            if(overlay == null) {
                overlayPurgeBuffer.Add(pair.Key);
                continue;
            }
            try {
                if(overlay.gameObject.activeSelf) overlay.gameObject.SetActive(false);
            } catch {
                overlayPurgeBuffer.Add(pair.Key);
            }
        }
        for(int i = 0; i < overlayPurgeBuffer.Count; i++) overlayMap.Remove(overlayPurgeBuffer[i]);
        overlayPurgeBuffer.Clear();
    }
    private static OverlayEntry GetOrCreateOverlay(PlanetRenderer renderer) {
        int id = renderer.GetInstanceID();
        if(overlayMap.TryGetValue(id, out OverlayEntry cached)) {
            if(cached?.Overlay != null) {
                if(cached.Source == null) cached.Source = PlanetSpriteRenderer(renderer);
                return cached;
            }
            overlayMap.Remove(id);
        }
        try {
            Transform existing = renderer.transform.Find(OverlayObjectName);
            SpriteRenderer overlay = existing != null
                ? existing.GetComponent<SpriteRenderer>() ?? existing.gameObject.AddComponent<SpriteRenderer>()
                : null;
            if(overlay == null) {
                GameObject host = new(OverlayObjectName);
                Transform t = host.transform;
                t.SetParent(renderer.transform, false);
                t.localPosition = new Vector3(0f, 0f, -0.01f);
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                overlay = host.AddComponent<SpriteRenderer>();
            }
            OverlayEntry entry = new() {
                Overlay = overlay,
                Source = PlanetSpriteRenderer(renderer),
            };
            overlayMap[id] = entry;
            return entry;
        } catch(Exception ex) {
            MainCore.Log.Msg("[PlanetColors] overlay create failed: " + ex.Message);
            return null;
        }
    }
    private static SpriteRenderer PlanetSpriteRenderer(PlanetRenderer renderer) {
        try {
            PlanetSprite sprite = renderer.sprite;
            if(sprite == null) return null;
            return sprite.GetComponent<SpriteRenderer>();
        } catch {
            return null;
        }
    }
    private static Sprite LoadOverlaySprite(string path) {
        if(string.IsNullOrWhiteSpace(path)) return null;
        string full = ResolveOverlayPath(path);
        if(string.IsNullOrEmpty(full)) return null;
        if(overlaySprites.TryGetValue(full, out Sprite cached)) return cached;
        Sprite made = null;
        try {
            if(!File.Exists(full)) {
                MainCore.Log.Msg("[PlanetColors] overlay image not found: " + full);
            } else {
                Texture2D tex = new(2, 2, TextureFormat.RGBA32, false) {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                if(tex.LoadImage(File.ReadAllBytes(full))) {
                    made = Sprite.Create(
                        tex,
                        new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );
                } else {
                    UnityEngine.Object.Destroy(tex);
                    MainCore.Log.Msg("[PlanetColors] overlay image unreadable: " + full);
                }
            }
        } catch(Exception ex) {
            MainCore.Log.Msg("[PlanetColors] overlay load failed (" + full + "): " + ex.Message);
            made = null;
        }
        overlaySprites[full] = made;
        return made;
    }
    private static string ResolveOverlayPath(string path) {
        try {
            if(Path.IsPathRooted(path)) return path;
            string levelPath = null;
            try { levelPath = ADOBase.levelPath; } catch(Exception e) { Diag.Ignore(e); }
            if(!string.IsNullOrEmpty(levelPath)) {
                string dir = Path.GetDirectoryName(levelPath);
                if(!string.IsNullOrEmpty(dir)) return Path.Combine(dir, path);
            }
            string root = MainCore.Paths?.RootPath;
            return string.IsNullOrEmpty(root) ? path : Path.Combine(root, path);
        } catch {
            return path;
        }
    }
}

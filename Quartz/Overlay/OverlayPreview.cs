using UnityEngine;
#if !IL2CPP
using Quartz.Core;
using Quartz.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
#endif
namespace Quartz.Overlay;
/// <summary>
/// A preview pane borrows an overlay's own canvas: it is switched to world space far off-world on a layer no game camera
/// draws, a private camera renders it into a texture the pane shows, and Release gives everything back. An overlay shows
/// itself while <see cref="Is"/> holds, with sample values outside a level. One lease at a time: one pane shows at once.
/// Names UnityEngine and UIElements types only, so it lives outside FacetUi.
/// </summary>
internal static class OverlayPreview {
    /// <summary>The overlay id being previewed, or null.</summary>
    internal static string Target { get; private set; }
    internal static bool Is(string id) => Target != null && Target == id;
#if IL2CPP
    internal static void Register(string id, Func<Canvas> canvas, Func<IEnumerable<RectTransform>> frame, Action begin = null, Action end = null,
        Func<GameObject> shown = null) { }
    internal static void Unregister(string id) { }
#else
    internal sealed class Source(Func<Canvas> canvas, Func<IEnumerable<RectTransform>> frame, Action begin, Action end, Func<GameObject> shown) {
        internal readonly Func<Canvas> Canvas = canvas;
        internal readonly Func<IEnumerable<RectTransform>> Frame = frame;
        internal readonly Action Begin = begin, End = end;
        internal readonly Func<GameObject> Shown = shown ?? (() => frame()?.FirstOrDefault() is { } r && r != null ? r.gameObject : null);
    }
    private static readonly Dictionary<string, Source> sources = new(StringComparer.Ordinal);
    private static Lease current;
    private static readonly Vector3[] corners = new Vector3[4];
    // Off-world, where no game camera looks even on a layer it draws.
    private static readonly Vector3 Park = new(0f, -20000f, 0f);

    /// <summary>The overlay's canvas and the rects to frame. begin runs before a lease reads the canvas (an overlay that
    /// builds on demand), end after it is released. shown is the object the overlay shows and hides (the first frame rect's
    /// when null).</summary>
    internal static void Register(string id, Func<Canvas> canvas, Func<IEnumerable<RectTransform>> frame, Action begin = null, Action end = null,
        Func<GameObject> shown = null) {
        if(Target == id) current?.Release();
        sources[id] = new Source(canvas, frame, begin, end, shown);
    }
    /// <summary>First in an overlay's Dispose: a borrowed canvas goes back before the overlay keeps its position or is destroyed.</summary>
    internal static void Unregister(string id) {
        if(Target == id) current?.Release();
        sources.Remove(id);
    }
    /// <summary>A lease on the overlay's canvas, or null: not registered, not built, an empty frame, arranging, or another lease out.</summary>
    internal static Lease Take(string id) {
        if(Target != null || id == null || UICore.IsArranging || !sources.TryGetValue(id, out Source s)) return null;
        Lease lease = null;
        try {
            s.Begin?.Invoke();
            Canvas canvas = s.Canvas();
            if(canvas != null && Bounds(s.Frame(), out _, out _)) lease = current = new Lease(id, s, canvas);
        } finally {
            if(lease == null) s.End?.Invoke();   // nothing borrowed, or a step threw: the overlay hears its preview end
        }
        return lease;
    }
#if DEBUG
    /// <summary>The self-test's view of what a lease on id borrows, and the time spent in Render.</summary>
    internal static Lease Current => current;
    internal static Canvas CanvasOf(string id) => sources.TryGetValue(id, out Source s) ? s.Canvas() : null;
    internal static GameObject ShownOf(string id) => sources.TryGetValue(id, out Source s) ? s.Shown() : null;
    internal static IEnumerable<RectTransform> FrameOf(string id) => sources.TryGetValue(id, out Source s) ? s.Frame() : null;
    /// <summary>Walks of a borrowed canvas made because its hierarchy changed.</summary>
    internal static int Relayers;
    internal static long RenderTicks;
    internal static int Renders;
    /// <summary>Frames read through an enumerator, which allocates: a render reads a list by index.</summary>
    internal static int Enumerated;
    /// <summary>The next lease throws once it has borrowed the canvas and made its camera.</summary>
    internal static bool FailNextLease;
#endif
    // The world-space union of the rects' corners; false when there is nothing to frame. Indexed for a list: Render runs
    // per frame, and a foreach over an interface allocates its enumerator.
    private static bool Bounds(IEnumerable<RectTransform> rects, out Vector3 min, out Vector3 max) {
        min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        if(rects is IList<RectTransform> list)
            for(int i = 0; i < list.Count; i++) Grow(list[i], ref min, ref max);
        else if(rects != null) {
#if DEBUG
            Enumerated++;
#endif
            foreach(RectTransform r in rects) Grow(r, ref min, ref max);
        }
        return max.x > min.x && max.y > min.y;
    }
    private static void Grow(RectTransform r, ref Vector3 min, ref Vector3 max) {
        if(r == null) return;
        r.GetWorldCorners(corners);
        foreach(Vector3 c in corners) {
            min = Vector3.Min(min, c);
            max = Vector3.Max(max, c);
        }
    }
    // Prefer 31, else the highest spare layer no camera draws; re-picked per lease, since another scene's cameras differ.
    private static int PickLayer() {
        Camera[] cams = Camera.allCameras;
        for(int layer = 31; layer >= 22; layer--) {
            int bit = 1 << layer;
            if(!cams.Any(c => c != null && (c.cullingMask & bit) != 0)) return layer;
        }
        return 31;   // ponytail: every spare layer is drawn somewhere; the park keeps the canvas out of those cameras' view
    }

    internal sealed class Lease {
        internal Canvas Canvas { get; }
        internal int Layer { get; }
        internal RenderTexture Texture { get; private set; }
        internal bool Released { get; private set; }
        private readonly Source source;
        private readonly RectTransform rect;
        private readonly RenderMode mode;
        private readonly Vector3 scale, position;
        private readonly Quaternion rotation;
        private readonly Vector2 size;
        // Every object put on the preview layer, with the layer it had: those found at the start, then any made later.
        private readonly List<Transform> tree = [];
        private readonly List<int> layers = [];
        private int hierarchy;
        private readonly Camera cam;
        private readonly Action<bool> onArranging;
        private readonly UnityEngine.Events.UnityAction<Scene, Scene> onScene;
        private VisualElement stage;

        internal Lease(string id, Source source, Canvas canvas) {
            this.source = source;
            Canvas = canvas;
            rect = (RectTransform)canvas.transform;
            mode = canvas.renderMode;
            (scale, position, rotation, size) = (rect.localScale, rect.localPosition, rect.localRotation, rect.sizeDelta);
            Vector2 logical = rect.rect.size;   // read while still an overlay: the screen in canvas units
            canvas.GetComponentsInChildren(true, tree);
            foreach(Transform t in tree) layers.Add(t.gameObject.layer);
            Layer = PickLayer();
            GameObject go = null;
            bool borrowed = false;
            try {
                canvas.renderMode = RenderMode.WorldSpace;
                rect.localScale = Vector3.one;
                rect.rotation = Quaternion.identity;
                rect.sizeDelta = logical;
                rect.position = Park;
                foreach(Transform t in tree) t.gameObject.layer = Layer;
                go = new GameObject("QuartzPreviewCamera");
                go.transform.SetParent(MainCore.Root.transform, false);
                go.transform.rotation = Quaternion.identity;
                cam = go.AddComponent<Camera>();
                cam.enabled = false;   // rendered by hand, once per pane tick
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.cullingMask = 1 << Layer;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 1000f;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.useOcclusionCulling = false;
                hierarchy = rect.hierarchyCount;
#if DEBUG
                if(FailNextLease) {
                    FailNextLease = false;
                    throw new InvalidOperationException("self-test: a lease fails after borrowing the canvas");
                }
#endif
                borrowed = true;
            } finally {
                // A step that threw: what was changed goes back and the camera goes. Take ends the preview.
                if(!borrowed) {
                    try {
                        Restore();
                    } finally {
                        if(go != null) Object.Destroy(go);
                    }
                }
            }
            onArranging = arranging => { if(arranging) Release(); };
            onScene = (_, _) => Release();
            UICore.OnArrangingChanged += onArranging;
            SceneManager.activeSceneChanged += onScene;
            Target = id;
        }

        /// <summary>Draws the element into the stage at its true on-screen size, shrunk only to fit. clear is the stage's
        /// colour, opaque: UI drawn into a transparent texture comes out with its edges at alpha squared.</summary>
        internal void Render(VisualElement stage, Color clear) {
            if(Released || stage?.panel == null) return;
#if DEBUG
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            if(Canvas == null || cam == null) {   // destroyed under the lease
                Release();
                return;
            }
            Rect wb = stage.worldBound;
            if(!(wb.width >= 1f && wb.height >= 1f)) return;   // not laid out yet
            if(!Bounds(source.Frame(), out Vector3 min, out Vector3 max)) {   // nothing left to frame: the pane's note shows
                Release();
                return;
            }
            this.stage = stage;
            if(rect.hierarchyCount != hierarchy) Relayer();
            float ppp = stage.panel.scaledPixelsPerPoint;
            // Up to a multiple of 32 px: a window resize does not re-create the texture every frame, and it is never smaller than
            // the stage it stretches to.
            int w = Mathf.CeilToInt(wb.width * ppp / 32f) * 32, h = Mathf.CeilToInt(wb.height * ppp / 32f) * 32;
            if(Texture == null || Texture.width != w || Texture.height != h) {
                FreeTexture();
                Texture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "QuartzPreview" };
                cam.targetTexture = Texture;
                stage.style.backgroundImage = Background.FromRenderTexture(Texture);
            }
            // One canvas unit is one panel point, so half the stage's height in points is the element at its true size.
            float aspect = wb.width / wb.height;
            float fit = Mathf.Max((max.y - min.y) / 2f, (max.x - min.x) / 2f / aspect) * 1.15f;
            cam.orthographicSize = Mathf.Max(fit, wb.height / 2f);
            cam.aspect = aspect;
            Vector3 centre = (min + max) * 0.5f;
            cam.transform.position = new Vector3(centre.x, centre.y, centre.z - 100f);
            cam.backgroundColor = clear;
            cam.Render();
#if DEBUG
            RenderTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            Renders++;
#endif
        }

        /// <summary>Gives everything back: render mode, layers, transform, and the camera and texture go. Safe to call twice,
        /// and after the canvas is destroyed.</summary>
        internal void Release() {
            if(Released) return;
            Released = true;
            UICore.OnArrangingChanged -= onArranging;
            SceneManager.activeSceneChanged -= onScene;
            try {
                Restore();
            } finally {
                if(cam != null) Object.Destroy(cam.gameObject);
                FreeTexture();
                if(stage != null) stage.style.backgroundImage = StyleKeyword.Null;
                stage = null;
                if(current == this) {
                    current = null;
                    Target = null;
                }
                source.End?.Invoke();
            }
        }

        // An object made under the canvas while it is lent (a shadow switched on) starts on its own layer, which the camera
        // does not draw. Counting the hierarchy is cheap; the canvas is walked only when the count moved.
        private static readonly List<Transform> walk = [];
        private void Relayer() {
            hierarchy = rect.hierarchyCount;
            Canvas.GetComponentsInChildren(true, walk);
            foreach(Transform t in walk) {
                int layer = t.gameObject.layer;
                if(layer == Layer) continue;
                tree.Add(t);
                layers.Add(layer);
                t.gameObject.layer = Layer;
            }
            walk.Clear();
#if DEBUG
            Relayers++;
#endif
        }

        private void Restore() {
            if(Canvas == null) return;
            Canvas.renderMode = mode;   // the overlay mode re-drives the RectTransform; put it back anyway
            (rect.localScale, rect.localPosition, rect.localRotation, rect.sizeDelta) = (scale, position, rotation, size);
            // Backwards: an object recorded twice (moved off the layer by its owner meanwhile) ends on its first layer.
            for(int i = tree.Count - 1; i >= 0; i--) if(tree[i] != null) tree[i].gameObject.layer = layers[i];
        }

        private void FreeTexture() {
            if(Texture == null) return;
            if(cam != null) cam.targetTexture = null;
            Texture.Release();
            Object.Destroy(Texture);
            Texture = null;
        }
    }
#endif
}

using GTweens.Easings;
using MonsterLove.StateMachine;
using Quartz.Core;
using Quartz.Game.Stats;
using Quartz.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Quartz.Overlay;
public enum OverlayPopEdge {
    Nearest,
    Left,
    Right,
    Top,
    Bottom,
}
public static class OverlayMotion {
    public const float MinSeconds = 0.05f;
    public const float MaxSeconds = 3f;
    public static readonly Easing[] Eases = [
        Easing.Linear, Easing.OutSine, Easing.OutQuad, Easing.OutCubic, Easing.OutQuart,
        Easing.OutQuint, Easing.OutExpo, Easing.OutCirc, Easing.OutBack, Easing.OutElastic,
        Easing.OutBounce,
    ];
    private static readonly string[] CanvasNames = [
        "QuartzPanelsCanvas", "QuartzKeyViewerCanvas", "QuartzComboCanvas", "QuartzJudgementCanvas",
        "QuartzProgressBarCanvas", "QuartzSongTitleCanvas", "QuartzPracticeCanvas",
    ];
    private const float Margin = 8f;
    private enum Phase {
        Shown,
        Entering,
        Exiting,
        Hidden,
    }
    private sealed class Slot {
        internal RectTransform Rect;
        internal RectTransform Wrap;
        internal Vector2 Travel;
        internal OverlayPopEdge Edge;
        internal int Measured;
        internal bool Seen;
    }
    private sealed class Layer {
        internal Canvas Canvas;
        internal RectTransform Rect;
        internal readonly List<Slot> Slots = [];
        internal bool Hid;
        internal bool OutsideGame;
    }
    private static readonly List<Layer> layers = [];
    private static readonly List<RectTransform> scratch = [];
    private static GameObject runnerObject;
    private static int rootChildCount = -1;
    private static int generation;
    private static Phase phase = Phase.Shown;
    private static float fraction;
    private static float fromFraction;
    private static float startTime;
    private static States? lastState;
    private static bool hasLastState;
    private static Easing easeKind = Easing.OutCubic;
    private static EasingDelegate ease = PresetEasingDelegateFactory.GetEaseDelegate(Easing.OutCubic);
    public static void Attach() {
        if(runnerObject != null || MainCore.Root == null) return;
        runnerObject = new GameObject("QuartzOverlayMotion");
        runnerObject.transform.SetParent(MainCore.Root.transform, false);
        runnerObject.AddComponent<OverlayMotionRunner>();
    }
    public static void Detach() {
        UnwrapAll();
        ShowAll();
        layers.Clear();
        rootChildCount = -1;
        phase = Phase.Shown;
        fraction = 0f;
        hasLastState = false;
        lastState = null;
        if(runnerObject != null) Object.Destroy(runnerObject);
        runnerObject = null;
    }
    internal static void UnwrapAll() {
        for(int i = 0; i < layers.Count; i++) {
            List<Slot> slots = layers[i].Slots;
            for(int j = 0; j < slots.Count; j++) Unwrap(slots[j]);
        }
    }
    private static void Wrap(Slot slot, Layer layer) {
        if(slot.Wrap != null || slot.Rect == null) return;
        int index = slot.Rect.GetSiblingIndex();
        GameObject obj = new("QuartzPopLayer");
        RectTransform wrap = obj.AddComponent<RectTransform>();
        wrap.SetParent(layer.Rect, false);
        wrap.anchorMin = Vector2.zero;
        wrap.anchorMax = Vector2.one;
        wrap.offsetMin = Vector2.zero;
        wrap.offsetMax = Vector2.zero;
        wrap.pivot = new Vector2(0.5f, 0.5f);
        wrap.SetSiblingIndex(index);
        obj.AddComponent<OverlayPopLayer>().Content = slot.Rect;
        slot.Rect.SetParent(wrap, false);
        slot.Wrap = wrap;
    }
    private static void Unwrap(Slot slot) {
        if(slot.Wrap == null) return;
        RectTransform wrap = slot.Wrap;
        slot.Wrap = null;
        int index = wrap.GetSiblingIndex();
        if(slot.Rect != null && wrap.parent != null) {
            slot.Rect.SetParent(wrap.parent, false);
            slot.Rect.SetSiblingIndex(index);
        }
        Object.Destroy(wrap.gameObject);
    }
    internal static void Drive() {
        OverlaySettings conf = OverlaySwitch.Conf;
        if(conf == null || !conf.PopEnabled) {
            UnwrapAll();
            ShowAll();
            phase = Phase.Shown;
            fraction = 0f;
            hasLastState = false;
            return;
        }
        PollState(conf);
        Advance(conf);
        Apply(conf);
    }
    private static void PollState(OverlaySettings conf) {
        States? now = ReadState();
        if(hasLastState && now == lastState) return;
        hasLastState = true;
        lastState = now;
        if(now is not States state) {
            phase = Phase.Shown;
            fraction = 0f;
            return;
        }
        switch(state) {
            case States.Start:
                phase = Phase.Hidden;
                fraction = 1f;
                break;
            case States.Countdown:
            case States.Checkpoint:
                BeginEnter();
                break;
            case States.PlayerControl:
                if(phase is Phase.Hidden or Phase.Exiting) BeginEnter();
                break;
            case States.Fail:
            case States.Won:
                BeginExit(conf);
                break;
        }
    }
    private static States? ReadState() {
        try {
            scrController controller = scrController.instance;
            if(controller == null || !controller.gameworld) return null;
            return ((StateBehaviour)controller).stateMachine.GetState() is States state ? state : null;
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    private static void BeginEnter() {
        if(phase == Phase.Entering) return;
        fromFraction = phase == Phase.Exiting ? fraction : 1f;
        startTime = Time.unscaledTime;
        phase = Phase.Entering;
        generation++;
    }
    private static void BeginExit(OverlaySettings conf) {
        if(!conf.PopExit || phase is Phase.Exiting or Phase.Hidden) return;
        fromFraction = fraction;
        startTime = Time.unscaledTime;
        phase = Phase.Exiting;
        generation++;
    }
    private static void Advance(OverlaySettings conf) {
        if(phase == Phase.Shown) {
            fraction = 0f;
            return;
        }
        if(phase == Phase.Hidden) {
            fraction = 1f;
            return;
        }
        if(easeKind != conf.PopEase) {
            easeKind = conf.PopEase;
            ease = PresetEasingDelegateFactory.GetEaseDelegate(easeKind);
        }
        float seconds = Mathf.Clamp(conf.PopSeconds, MinSeconds, MaxSeconds);
        float t = (Time.unscaledTime - startTime) / seconds;
        if(t >= 1f) {
            bool entering = phase == Phase.Entering;
            phase = entering ? Phase.Shown : Phase.Hidden;
            fraction = entering ? 0f : 1f;
            return;
        }
        fraction = phase == Phase.Entering
            ? fromFraction * (1f - ease(0f, 1f, t))
            : fromFraction + ((1f - fromFraction) * (1f - ease(0f, 1f, 1f - t)));
    }
    private static void Apply(OverlaySettings conf) {
        EnsureLayers();
        bool inGame = GameStats.InGame;
        if(!inGame) MarkOutsideGame();
        if(!OverlaySwitch.Enabled || !inGame || UICore.IsOpen || UICore.IsReorganizing) {
            UnwrapAll();
            ShowAll();
            return;
        }
        for(int i = 0; i < layers.Count; i++) {
            Layer layer = layers[i];
            if(layer.OutsideGame) {
                UnwrapLayer(layer);
                Show(layer);
                continue;
            }
            if(phase == Phase.Hidden) {
                UnwrapLayer(layer);
                Hide(layer);
                continue;
            }
            Show(layer);
            if(phase == Phase.Shown) UnwrapLayer(layer);
            else ApplyLayer(layer, conf.PopEdge);
        }
    }
    private static void MarkOutsideGame() {
        for(int i = 0; i < layers.Count; i++) {
            Layer layer = layers[i];
            if(layer.Rect == null) continue;
            bool visible = false;
            for(int j = 0; j < layer.Rect.childCount && !visible; j++)
                visible = layer.Rect.GetChild(j).gameObject.activeInHierarchy;
            layer.OutsideGame = visible && layer.Canvas != null && layer.Canvas.enabled;
        }
    }
    private static void EnsureLayers() {
        GameObject rootObject = MainCore.Root;
        if(rootObject == null) {
            layers.Clear();
            rootChildCount = -1;
            return;
        }
        Transform root = rootObject.transform;
        bool stale = root.childCount != rootChildCount;
        for(int i = 0; i < layers.Count && !stale; i++)
            if(layers[i].Canvas == null || layers[i].Rect == null) stale = true;
        if(!stale) return;
        UnwrapAll();
        ShowAll();
        layers.Clear();
        rootChildCount = root.childCount;
        for(int i = 0; i < root.childCount; i++) {
            Transform child = root.GetChild(i);
            if(child is not RectTransform rect) continue;
            if(Array.IndexOf(CanvasNames, child.name) < 0) continue;
            if(!child.TryGetComponent(out Canvas canvas)) continue;
            layers.Add(new Layer { Canvas = canvas, Rect = rect });
        }
    }
    private static void ApplyLayer(Layer layer, OverlayPopEdge edge) {
        scratch.Clear();
        for(int i = 0; i < layer.Rect.childCount; i++) {
            if(layer.Rect.GetChild(i) is not RectTransform child) continue;
            if(child.TryGetComponent(out OverlayPopLayer wrapper)) {
                if(wrapper.Content != null) scratch.Add(wrapper.Content);
                continue;
            }
            scratch.Add(child);
        }
        for(int i = 0; i < layer.Slots.Count; i++) layer.Slots[i].Seen = false;
        for(int i = 0; i < scratch.Count; i++) {
            RectTransform rect = scratch[i];
            Slot slot = SlotFor(layer, rect);
            slot.Seen = true;
            if(!rect.gameObject.activeInHierarchy) {
                Unwrap(slot);
                slot.Measured = 0;
                continue;
            }
            Wrap(slot, layer);
            if(slot.Wrap == null) continue;
            bool fresh = slot.Measured != generation;
            if(Offscreen(rect, slot.Wrap, layer.Rect, fresh ? edge : slot.Edge, out Vector2 travel, out OverlayPopEdge used)) {
                if(fresh) {
                    slot.Edge = used;
                    slot.Travel = travel;
                    slot.Measured = generation;
                } else if(travel.sqrMagnitude > slot.Travel.sqrMagnitude) {
                    slot.Travel = travel;
                }
            } else if(fresh) {
                continue;
            }
            slot.Wrap.anchoredPosition = slot.Travel * fraction;
        }
        for(int i = layer.Slots.Count - 1; i >= 0; i--) {
            Slot slot = layer.Slots[i];
            if(slot.Seen && slot.Rect != null) continue;
            Unwrap(slot);
            layer.Slots.RemoveAt(i);
        }
    }
    private static void UnwrapLayer(Layer layer) {
        for(int i = 0; i < layer.Slots.Count; i++) Unwrap(layer.Slots[i]);
    }
    private static Slot SlotFor(Layer layer, RectTransform rect) {
        for(int i = 0; i < layer.Slots.Count; i++)
            if(ReferenceEquals(layer.Slots[i].Rect, rect)) return layer.Slots[i];
        Slot slot = new() { Rect = rect };
        layer.Slots.Add(slot);
        return slot;
    }
    private static void Hide(Layer layer) {
        if(layer.Canvas == null || !layer.Canvas.enabled) return;
        layer.Canvas.enabled = false;
        layer.Hid = true;
    }
    private static void Show(Layer layer) {
        if(!layer.Hid) return;
        layer.Hid = false;
        if(layer.Canvas != null) layer.Canvas.enabled = true;
    }
    private static void ShowAll() {
        for(int i = 0; i < layers.Count; i++) Show(layers[i]);
    }
    private static bool Offscreen(
        RectTransform rect, RectTransform wrap, RectTransform canvas, OverlayPopEdge edge,
        out Vector2 travel, out OverlayPopEdge used
    ) {
        travel = Vector2.zero;
        used = edge;
        Rect screen = ScreenRect(canvas);
        if(screen.width <= 1f || screen.height <= 1f) return false;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(wrap, rect);
        if(bounds.size.x <= 0f && bounds.size.y <= 0f) return false;
        float xMin = bounds.min.x;
        float xMax = bounds.max.x;
        float yMin = bounds.min.y;
        float yMax = bounds.max.y;
        if(edge == OverlayPopEdge.Nearest) edge = NearestEdge(xMin, xMax, yMin, yMax, screen);
        used = edge;
        travel = edge switch {
            OverlayPopEdge.Right => new Vector2(Mathf.Max(0f, screen.xMax - xMin) + Margin, 0f),
            OverlayPopEdge.Top => new Vector2(0f, Mathf.Max(0f, screen.yMax - yMin) + Margin),
            OverlayPopEdge.Bottom => new Vector2(0f, -(Mathf.Max(0f, yMax - screen.yMin) + Margin)),
            _ => new Vector2(-(Mathf.Max(0f, xMax - screen.xMin) + Margin), 0f),
        };
        return true;
    }
    private static Rect ScreenRect(RectTransform canvas) {
        float scale = canvas.lossyScale.x;
        if(scale <= 0.0001f) return canvas.rect;
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        return new Rect(-width * 0.5f, -height * 0.5f, width, height);
    }
    private static OverlayPopEdge NearestEdge(float xMin, float xMax, float yMin, float yMax, Rect screen) {
        float left = xMin - screen.xMin;
        float right = screen.xMax - xMax;
        float bottom = yMin - screen.yMin;
        float top = screen.yMax - yMax;
        float best = left;
        OverlayPopEdge edge = OverlayPopEdge.Left;
        if(right < best) {
            best = right;
            edge = OverlayPopEdge.Right;
        }
        if(bottom < best) {
            best = bottom;
            edge = OverlayPopEdge.Bottom;
        }
        if(top < best) edge = OverlayPopEdge.Top;
        return edge;
    }
}
internal sealed class OverlayPopLayer : MonoBehaviour {
    internal RectTransform Content;
}
internal sealed class OverlayMotionRunner : MonoBehaviour {
    private void LateUpdate() {
        try {
            OverlayMotion.Drive();
        } catch(Exception e) {
            MainCore.Log.Err($"[Overlay] the pop-in animation threw: {e}");
        }
    }
    private void OnDisable() => OverlayMotion.UnwrapAll();
}

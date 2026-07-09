using Quartz.UI.Generator;
using Quartz.UI.Objects;
using UnityEngine;
namespace Quartz.UI.Panes;
internal sealed class PaneState {
    internal static event Action Changed;
    private static void RaiseChanged() => Changed?.Invoke();
    private RectTransform outer;
    public RectTransform Root { get; private set; }
    public bool HasContent { get; private set; }
    private readonly List<UIObject> tracked = [];
    public void Attach(RectTransform outerRect, RectTransform root) {
        outer = outerRect;
        Root = root;
        HasContent = false;
        tracked.Clear();
        if(outer != null) outer.gameObject.SetActive(false);
    }
    public void SetContent(Action<RectTransform, List<UIObject>> builder) {
        if(Root == null) return;
        DisposeTracked();
        GenerateUI.ClearChildren(Root);
        builder(Root, tracked);
        HasContent = true;
        if(outer != null) outer.gameObject.SetActive(true);
        RaiseChanged();
    }
    public void Clear() {
        if(Root == null) return;
        if(!HasContent) return;
        DisposeTracked();
        GenerateUI.ClearChildren(Root);
        HasContent = false;
        if(outer != null) outer.gameObject.SetActive(false);
        RaiseChanged();
    }
    private void DisposeTracked() {
        foreach(UIObject obj in tracked) obj?.Dispose();
        tracked.Clear();
    }
}
public static class ContextPane {
    private static readonly PaneState state = new();
    public static RectTransform Root => state.Root;
    public static bool HasContent => state.HasContent;
    internal static void Attach(RectTransform outer, RectTransform root) => state.Attach(outer, root);
    public static void SetContent(Action<RectTransform, List<UIObject>> builder) => state.SetContent(builder);
    public static void Clear() => state.Clear();
}
public static class LivePreviewPane {
    private static readonly PaneState state = new();
    public static RectTransform Root => state.Root;
    public static bool HasContent => state.HasContent;
    internal static void Attach(RectTransform outer, RectTransform root) => state.Attach(outer, root);
    public static void SetContent(Action<RectTransform, List<UIObject>> builder) => state.SetContent(builder);
    public static void Clear() => state.Clear();
}

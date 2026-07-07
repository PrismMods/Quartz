using Quartz.UI.Generator;
using Quartz.UI.Objects;
using UnityEngine;

namespace Quartz.UI.Panes;

// Backing implementation shared by ContextPane and LivePreviewPane below —
// same lifecycle for both, so it lives in one place instead of being
// duplicated per pane. Not exposed directly; each pane's static class forwards
// to its own private instance.
internal sealed class PaneState {
    // Fired whenever any pane gains or loses content, so the dock host (UICore)
    // can collapse the bottom band when both panes are empty and reveal it
    // again when content arrives — animated. Static: both panes share it.
    internal static event Action Changed;
    private static void RaiseChanged() => Changed?.Invoke();

    // The pane's own container, toggled active/inactive so an empty pane
    // takes no space in the dock's VerticalLayoutGroup.
    private RectTransform outer;
    // Where builders construct content. For a scrollable pane this is the
    // scroll view's inner content rect, not `outer` itself.
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

    // Builder constructs fresh widgets directly under Root (UIObject/GenerateUI
    // widgets don't support being reparented) and populates `tracked` with any
    // UIObject it creates via GenerateUI.* so they get Disposed (stopping
    // tweens/tickables) before their GameObjects are destroyed on the next
    // SetContent/Clear — skipping that step leaves stale tickables ticking
    // destroyed components.
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

// Docked pane for per-selection editing controls (e.g. a Key Viewer key's
// color/font editor), replacing what used to be an inline modal popup. Content
// is pushed in by whichever page currently owns the selection and cleared on
// tab switch (see UICore's MenuFactory.OnStateChanged subscription).
public static class ContextPane {
    private static readonly PaneState state = new();

    public static RectTransform Root => state.Root;
    public static bool HasContent => state.HasContent;

    internal static void Attach(RectTransform outer, RectTransform root) => state.Attach(outer, root);
    public static void SetContent(Action<RectTransform, List<UIObject>> builder) => state.SetContent(builder);
    public static void Clear() => state.Clear();
}

// Docked pane for passive, real-time mirrored state (e.g. a key flashing as
// it's physically pressed while you edit it) — deliberately separate from
// ContextPane so live feedback never gets torn down by an editing-control
// rebuild, or vice versa.
public static class LivePreviewPane {
    private static readonly PaneState state = new();

    public static RectTransform Root => state.Root;
    public static bool HasContent => state.HasContent;

    // outer is the LayoutElement-driven slot toggled active/inactive to
    // collapse the pane's space in the dock; root is where builders attach
    // content (e.g. an inset card inside that slot) — same outer/root split
    // as ContextPane, just usually the same visual footprint since this pane
    // isn't scrollable.
    internal static void Attach(RectTransform outer, RectTransform root) => state.Attach(outer, root);
    public static void SetContent(Action<RectTransform, List<UIObject>> builder) => state.SetContent(builder);
    public static void Clear() => state.Clear();
}

using Quartz.Features.KeyViewer.Layout;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects;
using Quartz.UI.Objects.Impl;
using UnityEngine;
namespace Quartz.UI.Editor;
internal sealed partial class KvInspector {
    private const float StatW = 100f;
    private const float StatH = 30f;
    private const float GraphW = 200f;
    private const float GraphH = 100f;
    private const float ChevronScale = 2f;
    private UIButton undoButton;
    private UIButton redoButton;
    private readonly List<UIObject> selectionScoped = [];
    internal void BuildToolbar(RectTransform bar) {
        RectTransform host = PopupHost(bar);
        RectTransform create = KvToolbar.Pill(bar);
        UIButton add = KvToolbar.Icon(
            create, UISprite.Layer128, "kvi_add", null,
            "DESC_KVI_ADD", "Add a key, a readout or a KPS graph to the middle of the view."
        );
        add.OnClick = () => KvPopup.Show(
            host, add.Rect,
            [
                ("KVI_ADD_KEY", "Add Key"),
                ("KVI_ADD_STAT", "Add Stat"),
                ("KVI_ADD_GRAPH", "Add Graph"),
            ],
            index => canvas.AddElement(NewElement(index switch {
                1 => KvElementKind.Stat,
                2 => KvElementKind.Graph,
                _ => KvElementKind.Key,
            }))
        );
        RectTransform element = KvToolbar.Pill(bar);
        Scoped(KvToolbar.Icon(
            element, UISprite.PlusBold128, "kvi_duplicate", canvas.DuplicateSelection,
            "DESC_KVI_DUPLICATE", "Copy the selected elements, offset slightly from the originals."
        ));
        UIButton front = KvToolbar.Icon(
            element, UISprite.ChevronDown128, "kvi_z_front", () => canvas.NudgeZ(true),
            "DESC_KVI_Z_FRONT", "Draw the selected elements one step in front of the ones they overlap.",
            ChevronScale
        );
        Scoped(front);
        Scoped(KvToolbar.Icon(
            element, UISprite.ChevronDown128, "kvi_z_back", () => canvas.NudgeZ(false),
            "DESC_KVI_Z_BACK", "Draw the selected elements one step behind the ones they overlap.",
            ChevronScale
        ));
        RectTransform view = KvToolbar.Pill(bar);
        undoButton = KvToolbar.Icon(
            view, UISprite.TurnArrow128, "kvi_undo", canvas.Undo,
            "DESC_KVI_UNDO", "Step back through your edits."
        );
        redoButton = KvToolbar.Icon(
            view, UISprite.TurnArrow128, "kvi_redo", canvas.Redo,
            "DESC_KVI_REDO", "Step forward again through the edits you undid."
        );
        KvToolbar.Icon(
            view, UISprite.Minus128, "kvi_zoom_out", () => canvas.ZoomBy(false),
            "DESC_KVI_ZOOM_OUT", "Zoom the view out. The - key over the canvas does the same."
        );
        KvToolbar.Icon(
            view, UISprite.Plus128, "kvi_zoom_in", () => canvas.ZoomBy(true),
            "DESC_KVI_ZOOM_IN", "Zoom the view in. The + key over the canvas does the same."
        );
        KvToolbar.Icon(
            view, UISprite.Reset128, "kvi_zoom_reset", canvas.ResetView,
            "DESC_KVI_ZOOM_RESET", "Back to actual size, at the top-left of the layout. The 0 key does the same."
        );
        KvToolbar.Icon(
            view, UISprite.Grid128, "kvi_zoom_fit", canvas.FitToContent,
            "DESC_KVI_ZOOM_FIT", "Frame every element on this tab. Use this if you have panned the layout out of sight."
        );
        RectTransform destructive = KvToolbar.Pill(bar);
        Scoped(KvToolbar.Icon(
            destructive, UISprite.Eraser128, "kvi_delete", canvas.DeleteSelection,
            "DESC_KVI_DELETE", "Remove the selected elements from this tab."
        ));
        Flip(redoButton.Rect, -1f, 1f);
        Flip(front.Rect, 1f, -1f);
    }
    private static RectTransform PopupHost(RectTransform bar) => KvToolbar.RegionOf(bar);
    private static void Flip(RectTransform button, float x, float y) {
        if(button.childCount == 0) return;
        button.GetChild(0).localScale = new Vector3(x, y, 1f);
    }
    internal void SyncToolbar() {
        bool any = canvas.Selection.Count > 0;
        foreach(UIObject obj in selectionScoped) obj.SetBlocked(!any, true);
        undoButton?.SetBlocked(!canvas.CanUndo, true);
        redoButton?.SetBlocked(!canvas.CanRedo, true);
    }
    private void BuildHiddenFlag(RectTransform root, List<UIObject> tracked) {
        UIToggle t = KvWidgets.Toggle(
            GenerateUI.Row(root), false, canvas.SelectionHidden(),
            canvas.SetSelectionHidden, "Hidden", "kvi_hidden"
        );
        t.Rect.AddToolTip(
            "DESC_KVI_HIDDEN",
            "Keep the selected elements in the layout but stop drawing them in game. Hidden elements dim on the canvas and cannot be clicked."
        );
        tracked.Add(t);
    }
    private void Scoped(UIObject obj) => selectionScoped.Add(obj);
    private KvElement NewElement(KvElementKind kind) {
        Vector2 at = canvas.ViewCenter();
        float w = kind switch {
            KvElementKind.Graph => GraphW,
            KvElementKind.Stat => StatW,
            _ => 60f,
        };
        float h = kind switch {
            KvElementKind.Graph => GraphH,
            KvElementKind.Stat => StatH,
            _ => 60f,
        };
        KvPresets.KvKeyStyle style = KvPresets.NewElementStyle(kind != KvElementKind.Key);
        KvElement el = KvElement.Wrap(
            KvPresets.NewPosition(Mathf.Round(at.x - (w * 0.5f)), Mathf.Round(at.y - (h * 0.5f)), w, h, style),
            kind
        );
        switch(kind) {
            case KvElementKind.Stat:
                el.StatType = "kps";
                break;
            case KvElementKind.Graph:
                el.Raw["graphType"] = "line";
                el.StatType = "kps";
                break;
        }
        return el;
    }
}

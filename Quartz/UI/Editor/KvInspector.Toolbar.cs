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
    /// <summary>
    /// The z-order arrows are DM Note's chevron, which it only ever draws inside a dropdown at 6x4.
    /// Used here as an action icon it needs to carry a button, so it is drawn up to roughly the
    /// footprint the rest of the set has.
    /// </summary>
    private const float ChevronScale = 2f;
    private UIButton undoButton;
    private UIButton redoButton;
    /// <summary>Controls that mean nothing without a selection, blocked out when there is none.</summary>
    private readonly List<UIObject> selectionScoped = [];
    /// <summary>
    /// The always-available actions, as DM Note's bar: grouping pills of icon buttons, with the
    /// multi-choice ones folded behind a list popup.
    ///
    /// The grouping is DM Note's rather than an arrangement of our own — create, then the element
    /// actions, then view, then the destructive one last. Two things here are Quartz's and not
    /// DM Note's, both deliberate: undo/redo and the zoom controls sit on the bar, where DM Note
    /// hangs them off its minimap and its context menu. They stay visible because they are the
    /// fallback for every navigation gesture — if the middle button, the right button and every
    /// modifier all fail, these still frame the layout.
    ///
    /// Add has to work from an empty canvas, so none of this can live in the per-selection
    /// inspector beside it. The host may append a pill of its own after these — the file actions
    /// are the page's, not this class's.
    /// </summary>
    internal void BuildToolbar(RectTransform bar) {
        RectTransform host = PopupHost(bar);
        RectTransform create = KvToolbar.Pill(bar);
        // Built without its handler, which is then set: the popup has to hang off this button's own
        // rect, and a lambda that closes over a variable it is being assigned to is both a
        // null-dereference warning and a trap for whoever moves it.
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
        // One glyph, mirrored, for each opposed pair — which is what DM Note does rather than ship a
        // second sprite that is the first one backwards. Applied after construction so the icons
        // above stay a flat list of what each button does.
        Flip(redoButton.Rect, -1f, 1f);
        Flip(front.Rect, 1f, -1f);
    }
    /// <summary>
    /// Where popups mount: the editor region, so a tray can hang above the bar rather than being
    /// clipped inside it (the bar is a scroll viewport now — see KvToolbar.RegionOf).
    /// </summary>
    private static RectTransform PopupHost(RectTransform bar) => KvToolbar.RegionOf(bar);
    /// <summary>Mirrors the glyph, not the button: the backdrop is square, but a flipped rect would
    /// also flip the tooltip's anchor and the hover it inherits.</summary>
    private static void Flip(RectTransform button, float x, float y) {
        if(button.childCount == 0) return;
        button.GetChild(0).localScale = new Vector3(x, y, 1f);
    }
    /// <summary>Re-read what the canvas owns. Driven by selection and document changes, never polled.</summary>
    internal void SyncToolbar() {
        bool any = canvas.Selection.Count > 0;
        foreach(UIObject obj in selectionScoped) obj.SetBlocked(!any, true);
        undoButton?.SetBlocked(!canvas.CanUndo, true);
        redoButton?.SetBlocked(!canvas.CanRedo, true);
    }
    /// <summary>
    /// The selection's own visibility. Written straight through rather than through
    /// <see cref="Edit"/> — SetSelectionHidden snapshots and repaints for itself, and a second
    /// snapshot around it would cost the user two undo steps for one click.
    /// </summary>
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
    /// <summary>
    /// A hand-added element, built through KvPresets so the seven fields DM Note's loader
    /// requires cannot be forgotten — one missing field fails the whole preset load, not just
    /// the element carrying it.
    /// </summary>
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

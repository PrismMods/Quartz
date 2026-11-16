#if !IL2CPP
using Facet;
using Quartz.Overlay;
using Quartz.Prism;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// An Overlay page's preview pane: the overlay's real element, borrowed through OverlayPreview while the pane shows and
/// drawn into its stage. A lease that fails or is taken back (a scene change, arranging, the overlay going away) is asked
/// for again every frame, and while there is none a note under the stage says why it is empty.
/// </summary>
internal static class FacetOverlayPreview {
    /// <summary>The pane, in the page's right column. Every Overlay feature's overlay id is its feature id. emptyKey says why
    /// the stage is empty, asked while it is (null: the element is not loaded yet). click, when given, hears a left click on
    /// the stage as the world point under it on the borrowed canvas.</summary>
    internal static VisualElement Pane(VisualElement right, string overlayId, Func<string> emptyKey = null, Action<Vector3> click = null) {
        emptyKey ??= () => "MENU_PREVIEW_IN_LEVEL";
        OverlayPreview.Lease lease = null;
        Label note = null;
        bool noteShown = true;
        string noteKey = emptyKey();
        void Follow() {
            bool want = lease == null || lease.Released;
            if(note == null) return;
            if(want && emptyKey() is var key && key != noteKey) Words.Label(note, noteKey = key);   // the reason changed while empty
            if(want == noteShown) return;
            noteShown = want;
            note.style.display = want ? DisplayStyle.Flex : DisplayStyle.None;
        }
        VisualElement stage = Controls.Preview(right, "MENU_PREVIEW", o => {
            o.Id = overlayId + "-preview";
            o.Aspect = 16f / 9f;
            o.EveryMs = 0;
            o.Show = _ => {
                lease = OverlayPreview.Take(overlayId);
                Follow();
            };
            o.Tick = s => {
                if(lease == null || lease.Released) lease = OverlayPreview.Take(overlayId);
                lease?.Render(s, Clear(s));
                Follow();
            };
            o.Hide = () => {
                lease?.Release();
                lease = null;
                Follow();
            };
            if(ShellModel.HasLayout(overlayId) && PrismBridge.UiAvailable) {
                o.Link = "MENU_EDIT_LAYOUT";
                o.OnLink = QuartzShell.EditLayout;
            }
        });
        if(click != null) {
            // A left click (press and release on the stage); the press's local position is the point.
            Clickable clickable = null;
            clickable = new Clickable(() => {
                Rect r = stage.layout;
                Vector2 at = clickable!.lastMousePosition;
                if(lease == null || lease.Released || r.width < 1f || r.height < 1f) return;
                if(lease.WorldAt(new Vector2(at.x / r.width, at.y / r.height)) is Vector3 world) click(world);
            });
            stage.AddManipulator(clickable);
        }
        // Under the stage, above the link; Facet's own Note is fixed text and cannot follow the lease.
        VisualElement pane = stage.parent;
        note = Ui.Muted(pane, noteKey);
        note.name = overlayId + "-preview-empty";
        note.style.marginTop = Theme.For(pane).Sizes.Gap / 2f;
        pane.Insert(pane.IndexOf(stage) + 1, note);
        noteShown = true;
        Follow();
        return stage;
    }
    /// <summary>The stage as it looks in the window, opaque: its Row tint over the Panel.</summary>
    internal static Color Clear(VisualElement stage) {
        Theme t = Theme.For(stage);
        Color row = t[Role.Row], c = Color.Lerp(t[Role.Panel], row, row.a);
        c.a = 1f;
        return c;
    }
}
#endif

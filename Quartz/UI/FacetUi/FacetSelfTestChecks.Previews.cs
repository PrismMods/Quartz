#if DEBUG && !IL2CPP
using System.Collections;
using System.Diagnostics;
using Facet;
using Quartz.Core;
using Quartz.Features.Combo;
using Quartz.Overlay;
using Quartz.Prism;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Overlay preview panes: a shown pane borrows the overlay's live canvas, draws it, follows a setting within 150 ms, and
/// gives the canvas back on hide, on arranging and when the overlay goes away, borrowing the rebuilt one after. PreviewChecks
/// is shared by the Overlay pages; here it runs on a scratch window holding only Combo's pane.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator Previews(Action<string, bool, string> check, Action<string> info) {
        string id = ComboOverlay.FontKey;
        TakeRefusals(check, id);
        TakeEndsOnThrow(check, id);
        yield return TakeRollsBack(check, id);
        float size = ComboOverlay.Conf.FontSize;
        try {
            // No Facet control on the scratch window: the overlay's own path, Conf then Apply. The element is looked up
            // each time, since the checks rebuild the overlay.
            yield return PreviewChecks(check, info, null, id,
                () => {
                    ComboOverlay.Conf.FontSize = size + 30f;
                    ComboOverlay.Apply();
                    return true;
                },
                () => OverlayPreview.ShownOf(id)?.transform.Find("Value")?.GetComponent<TextMeshProUGUI>() is { } value && value.fontSize >= size + 29f);
        } finally {
            ComboOverlay.Conf.FontSize = size;   // never saved meanwhile: Apply does not save
            ComboOverlay.Apply();
        }
    }

    // Take says no to an unknown id, an empty frame and a second lease, and borrows nothing when it does.
    private static void TakeRefusals(Action<string, bool, string> check, string id) {
        const string empty = "selftest-empty";
        Canvas canvas = OverlayPreview.CanvasOf(id);
        OverlayPreview.Register(empty, () => canvas, () => []);
        OverlayPreview.Lease first = null;
        try {
            bool unknown = OverlayPreview.Take("selftest-unknown") == null, none = OverlayPreview.Take(empty) == null;
            first = OverlayPreview.Take(id);
            bool second = first != null && OverlayPreview.Take(id) == null;
            check("preview: Take refuses an unknown id, an empty frame and a second lease",
                unknown && none && second && OverlayPreview.Target == id, $"unknown={unknown} empty={none} second={second} target={OverlayPreview.Target}");
        } finally {
            first?.Release();
            OverlayPreview.Unregister(empty);
        }
    }

    // An overlay whose canvas or frame throws once its preview has begun hears it end, once each time, and nothing is lent.
    private static void TakeEndsOnThrow(Action<string, bool, string> check, string id) {
        const string boom = "selftest-throws";
        Canvas canvas = OverlayPreview.CanvasOf(id);
        int begun = 0, ended = 0;
        var threw = new List<string>();
        void Try(Func<Canvas> c, Func<IEnumerable<RectTransform>> frame) {
            OverlayPreview.Register(boom, c, frame, () => begun++, () => ended++);
            try {
                OverlayPreview.Take(boom)?.Release();
            } catch(InvalidOperationException e) {
                threw.Add(e.Message);
            }
        }
        try {
            Try(() => throw new InvalidOperationException("self-test: canvas"), () => []);
            Try(() => canvas, () => throw new InvalidOperationException("self-test: frame"));
            check("preview: a canvas or frame that throws still ends the preview it began, and lends nothing",
                threw.Count == 2 && begun == 2 && ended == 2 && OverlayPreview.Target == null && OverlayPreview.Current == null,
                $"threw={string.Join("|", threw)} begun={begun} ended={ended} target={OverlayPreview.Target}");
        } finally {
            OverlayPreview.Unregister(boom);
        }
    }

    // A lease that throws after borrowing the canvas puts back what it changed and leaves no camera and no lease.
    private static IEnumerator TakeRollsBack(Action<string, bool, string> check, string id) {
        Canvas canvas = OverlayPreview.CanvasOf(id);
        GameObject root = OverlayPreview.ShownOf(id);
        if(canvas == null || root == null) {
            check("preview: a lease that throws puts the canvas back", false, $"canvas={canvas != null} root={root != null}");
            yield break;
        }
        var rect = (RectTransform)canvas.transform;
        var was = (canvas.renderMode, layer: root.layer, rect.localPosition, rect.localScale);
        string threw = null;
        OverlayPreview.FailNextLease = true;
        try {
            OverlayPreview.Take(id)?.Release();
        } catch(InvalidOperationException e) {
            threw = e.Message;
        } finally {
            OverlayPreview.FailNextLease = false;
        }
        yield return Frames(1);   // Destroy is deferred
        bool camera = GameObject.Find("QuartzPreviewCamera") != null;
        check("preview: a lease that throws after borrowing puts back the render mode, layers and transform, and destroys its camera",
            threw != null && OverlayPreview.Target == null && OverlayPreview.Current == null && !camera
            && (canvas.renderMode, root.layer, rect.localPosition, rect.localScale) == was,
            $"threw={threw != null} target={OverlayPreview.Target} camera={camera} mode={canvas.renderMode}/{was.renderMode} layer={root.layer}/{was.layer} "
            + $"at={rect.localPosition}/{was.localPosition}");
    }

    /// <summary>The pane of overlayId on pageKey (null: a scratch window holding only that pane, where the arranging step runs too).
    /// applySetting changes something the element shows; elementChanged reads it off the element, never off TMP's text, and
    /// looks the element up each time: the checks power Quartz off and on, which rebuilds the overlay. Visibility is read off
    /// the object the overlay registered as the one it shows.</summary>
    internal static IEnumerator PreviewChecks(Action<string, bool, string> check, Action<string> info, string pageKey, string overlayId,
        Func<bool> applySetting, Func<bool> elementChanged) {
        string tag = $"preview {pageKey ?? "scratch"} {overlayId}";
        // What the element was before any pane: a page left open by the checks before this one holds the lease.
        if(QuartzShell.Hide()) yield return Frames(1);
        Canvas canvas = OverlayPreview.CanvasOf(overlayId);
        GameObject root = OverlayPreview.ShownOf(overlayId);
        if(canvas == null || root == null) {
            check($"{tag}: the overlay is built", false, $"canvas={canvas != null} root={root != null} on={MainCore.IsModEnabled}");
            yield break;
        }
        bool rootWas = root.activeSelf;   // shown outside a level too where the overlay does (the Key Viewer's default)
        int layerWas = canvas.gameObject.layer, rootLayerWas = root.layer;
        Surface scratch = null;
        bool off = false;
        try {
            VisualElement surface;
            if(pageKey == null) {
                scratch = Window("QuartzSelfTestPreview", out Window w);
                FacetOverlayPreview.Pane(w.Body, overlayId);
                surface = scratch.Root;
            } else {
                QuartzShell.Open(pageKey);
                surface = QuartzShell.Surface.Root;
            }
            // Show
            bool Shown() => OverlayPreview.Is(overlayId) && canvas.renderMode == RenderMode.WorldSpace && root.activeInHierarchy
                && OverlayPreview.Current?.Texture != null;
            int frames = 0;
            for(; frames < 10 && !Shown(); frames++) yield return null;
            VisualElement stage = surface.Q(overlayId + "-preview"), note = surface.Q(overlayId + "-preview-empty");
            RenderTexture rt = OverlayPreview.Current?.Texture;
            float ppp = stage?.panel?.scaledPixelsPerPoint ?? 0f;
            Vector2 want = stage == null ? Vector2.zero : stage.worldBound.size * ppp;
            // Rounded up to 32 px: never smaller than the stage it stretches to.
            bool Fits(int got, float stagePx) => got + 0.5f >= stagePx && got - stagePx < 32f;
            check($"{tag}: a shown pane borrows the canvas within 10 frames: world space, the element shown, a texture the stage's size rounded up to 32 px, no note",
                Shown() && rt != null && Fits(rt.width, want.x) && Fits(rt.height, want.y) && note != null && !Displayed(note),
                $"frames={frames} target={OverlayPreview.Target} mode={canvas.renderMode} active={root.activeInHierarchy} "
                + $"rt={(rt == null ? "none" : rt.width + "x" + rt.height)} want={want.x:0}x{want.y:0} note={(note == null ? "none" : Displayed(note).ToString())}");
            info($"{tag}: texture {(rt == null ? "none" : rt.width + "x" + rt.height)} for a stage of {stage?.worldBound.size} pt at {ppp:0.###} px/pt, layer {OverlayPreview.Current?.Layer}");
            // Drawn
            yield return Frames(3);
            rt = OverlayPreview.Current?.Texture;
            Color clear = stage == null ? Color.clear : FacetOverlayPreview.Clear(stage);
            (int lit, Color32 corner) = Pixels(rt, clear);
            check($"{tag}: the element is drawn: more than 50 pixels differ from the stage colour", lit > 50, $"lit={lit}");
            Color32 c = clear;
            check($"{tag}: the stage colour is the window's Row over Panel, opaque: a corner pixel matches it (±2/255)",
                rt != null && Math.Abs(corner.r - c.r) <= 2 && Math.Abs(corner.g - c.g) <= 2 && Math.Abs(corner.b - c.b) <= 2 && corner.a == 255,
                $"corner={corner} clear={c}");
            // Going away: Quartz powered off with the pane shown. The overlay's Dispose gives the canvas back before it keeps
            // the position and destroys the canvas; powered on again, the pane borrows the new one. Switching the feature off
            // or on runs the same Dispose and Initialize. Before the setting change, so Dispose saves only the player's values.
            OverlayPreview.Lease had = OverlayPreview.Current;
            var placed = (RectTransform)root.transform;
            (Vector2 at, Vector3 scaled, Quaternion turned) = (placed.anchoredPosition, placed.localScale, placed.localRotation);
            off = true;
            MainCore.Runtime.SetModEnabled(false, false, save: false);
            check($"{tag}: Quartz powered off with the pane shown gives the canvas back in the same frame, before it is destroyed",
                OverlayPreview.Target == null && OverlayPreview.Current == null && had is { Released: true, Texture: null } && canvas != null
                && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.gameObject.layer == layerWas && root != null && root.layer == rootLayerWas,
                $"target={OverlayPreview.Target} released={had?.Released} texture={had?.Texture != null} canvas={canvas != null} "
                + $"mode={(canvas != null ? canvas.renderMode.ToString() : "gone")} layer={(canvas != null ? canvas.gameObject.layer : -1)}/{layerWas} "
                + $"rootLayer={(root != null ? root.layer : -1)}/{rootLayerWas}");
            yield return Frames(1);
            check($"{tag}: while Quartz is off the canvas, camera and texture are gone, no lease is taken and the note shows",
                canvas == null && GameObject.Find("QuartzPreviewCamera") == null && OverlayPreview.Target == null && note != null && Displayed(note),
                $"canvas={canvas != null} camera={GameObject.Find("QuartzPreviewCamera") != null} target={OverlayPreview.Target} note={note != null && Displayed(note)}");
            MainCore.Runtime.SetModEnabled(true, false, save: false);
            off = false;
            canvas = OverlayPreview.CanvasOf(overlayId);
            root = OverlayPreview.ShownOf(overlayId);
            if(canvas == null || root == null) {
                check($"{tag}: powered on again, the overlay is built again", false, $"canvas={canvas != null} root={root != null}");
                yield break;
            }
            layerWas = canvas.gameObject.layer;
            rootLayerWas = root.layer;
            placed = (RectTransform)root.transform;
            // A page whose feature went off and on with Quartz was rebuilt, with a new pane.
            stage = surface.Q(overlayId + "-preview");
            note = surface.Q(overlayId + "-preview-empty");
            for(frames = 0; frames < 10 && !Shown(); frames++) yield return null;
            Vector2 moved = placed.anchoredPosition - at;
            check($"{tag}: powered on again, the pane borrows the new canvas within 10 frames, and the element sits where its stored position put it (±0.5)",
                Shown() && !Displayed(note) && Mathf.Abs(moved.x) <= 0.5f && Mathf.Abs(moved.y) <= 0.5f && placed.localScale == scaled && placed.localRotation == turned,
                $"frames={frames} target={OverlayPreview.Target} mode={canvas.renderMode} texture={OverlayPreview.Current?.Texture != null} note={Displayed(note)} "
                + $"at={placed.anchoredPosition}/{at} scale={placed.localScale}/{scaled} rotation={placed.localRotation.eulerAngles}/{turned.eulerAngles}");
            // Update
            float t0 = Time.realtimeSinceStartup, ms = -1f;
            bool applied = applySetting();
            while(Time.realtimeSinceStartup - t0 < 1f) {
                if(elementChanged()) {
                    ms = (Time.realtimeSinceStartup - t0) * 1000f;
                    break;
                }
                yield return null;
            }
            check($"{tag}: a setting change shows on the element within 150 ms", applied && ms >= 0f && ms <= 150f, $"ms={ms:0.0}");
            // Cost
            OverlayPreview.RenderTicks = 0;
            OverlayPreview.Renders = 0;
            OverlayPreview.Relayers = 0;
            yield return Frames(120);
            double mean = OverlayPreview.Renders == 0 ? -1 : OverlayPreview.RenderTicks * 1000.0 / Stopwatch.Frequency / OverlayPreview.Renders;
            MainCore.Log.Msg($"[perf] preview render mean {mean:0.000} ms over {OverlayPreview.Renders} renders, {OverlayPreview.Relayers} canvas walks");
            check("[perf] preview render mean over 120 frames is at most 1 ms", OverlayPreview.Renders >= 100 && mean >= 0 && mean <= 1.0,
                $"mean={mean:0.000} ms renders={OverlayPreview.Renders} walks={OverlayPreview.Relayers}");
            // Garbage: a render runs every frame the pane shows, so it reads the frame by index, never through an enumerator, which
            // allocates. Counted, not measured: the game's Boehm GC leaves GC.GetAllocatedBytesForCurrentThread at 0 even for a new 4 KB array.
            OverlayPreview.Lease live = OverlayPreview.Current;
            int rendered = OverlayPreview.Renders;
            OverlayPreview.Enumerated = 0;
            for(int i = 0; live != null && i < 120; i++) live.Render(stage, clear);
            check("[perf] 120 preview renders read the frame by index, through no enumerator", OverlayPreview.Renders - rendered == 120 && OverlayPreview.Enumerated == 0,
                $"renders={OverlayPreview.Renders - rendered} enumerated={OverlayPreview.Enumerated}");
            // Arranging: a shared session needs the real element, at once. On the shell the session hides the menu instead.
            if(scratch != null) {
                // The session stays open while it is read, as a shared editor reads every box each frame.
                PrismBridge.TouchLayout();
                UICore.SetSharedLayout(true);
                bool back = OverlayPreview.Target == null && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.gameObject.layer == layerWas;
                check($"{tag}: arranging takes the canvas back in the same frame", back, $"target={OverlayPreview.Target} mode={canvas.renderMode} layer={canvas.gameObject.layer}/{layerWas}");
                for(int i = 0; i < 3; i++) {
                    PrismBridge.TouchLayout();
                    yield return null;
                }
                check($"{tag}: while arranging no lease is taken and the note says why the stage is empty",
                    UICore.SharedLayoutOpen && OverlayPreview.Target == null && note != null && Displayed(note),
                    $"session={UICore.SharedLayoutOpen} target={OverlayPreview.Target} note={note != null && Displayed(note)}");
                for(frames = 0; frames < 10 && UICore.SharedLayoutOpen; frames++) yield return null;   // no more reads: it ends
                for(frames = 0; frames < 10 && !Shown(); frames++) yield return null;
                check($"{tag}: once arranging ends the pane borrows the canvas again within 10 frames",
                    !UICore.SharedLayoutOpen && Shown() && note != null && !Displayed(note), $"frames={frames} session={UICore.SharedLayoutOpen}");
            }
            // Hide, from a live lease: the checks after it cannot pass on a pane that holds none.
            OverlayPreview.Lease holding = OverlayPreview.Current;
            RenderTexture held = holding?.Texture;
            check($"{tag}: right before hiding, the pane holds a live lease with a texture", holding != null && !holding.Released && held != null,
                $"lease={holding != null} released={holding?.Released} texture={held != null}");
            if(scratch != null) scratch.Visible = false;
            else QuartzShell.Hide();
            yield return Frames(1);
            check($"{tag}: hiding the pane gives everything back within 1 frame: render mode, layers, no camera, no texture, no target",
                canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.gameObject.layer == layerWas && root.gameObject.layer == rootLayerWas
                && GameObject.Find("QuartzPreviewCamera") == null && held == null && OverlayPreview.Target == null,
                $"mode={canvas.renderMode} layer={canvas.gameObject.layer}/{layerWas} camera={GameObject.Find("QuartzPreviewCamera") != null} texture={held != null} target={OverlayPreview.Target}");
            frames = 0;
            for(; frames < 3 && root.activeSelf != rootWas; frames++) yield return null;
            check($"{tag}: within 3 frames the element is shown or hidden as before the pane", root.activeSelf == rootWas,
                $"active={root.activeSelf} before={rootWas} frames={frames}");
        } finally {
            if(off) MainCore.Runtime.SetModEnabled(true, false, save: false);
            UICore.SetSharedLayout(false);
            scratch?.Dispose();
            if(pageKey != null) QuartzShell.Hide();   // first: a pane still shown borrows again on its next tick
            OverlayPreview.Current?.Release();
        }
    }

    // How many pixels of the texture differ from clear, and its bottom-left pixel.
    private static (int Lit, Color32 Corner) Pixels(RenderTexture rt, Color clear) {
        if(rt == null) return (0, default);
        RenderTexture was = RenderTexture.active;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        try {
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            Color32 c = clear;
            Color32[] px = tex.GetPixels32();
            int lit = px.Count(p => Math.Abs(p.r - c.r) > 2 || Math.Abs(p.g - c.g) > 2 || Math.Abs(p.b - c.b) > 2);
            return (lit, px[0]);
        } finally {
            RenderTexture.active = was;
            UnityEngine.Object.Destroy(tex);
        }
    }
}
#endif

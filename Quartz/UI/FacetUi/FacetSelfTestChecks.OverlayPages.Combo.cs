#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Quartz.Features;
using Quartz.Features.Combo;
using Quartz.Features.Interop;
using Quartz.Overlay;
using Quartz.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using HandOff = Quartz.UI.MenuHandOff;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Overlay ▸ Combo in the new menu: its rows against the classic page, its two columns' edges side by side and stacked, the
/// caption switch seen by the classic page, the no-pop hotkey, a font pick that keeps the preview, a new global or found
/// font reaching the font row, a shadow row's middle-click, and the preview pane.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string ComboPage = "overlay.combo";
    private static ComboSettings ComboConf => ComboOverlay.Conf;

    // Rows 1 to 32 of the classic page; the XPerfect row only where that mod is installed.
    private static string[] ComboRows() => [.. new[] {
        "COMBO_AUTO", "COMBO_XPERFECT", "COMBO_ANCHOR_LABEL", "FONT_OVERLAY", "COMBO_FONTSIZE",
        "COMBO_MASTER_SIZE", "COMBO_CAPTION", "COMBO_CAPTIONTEXT", "COMBO_CAPTION_SIZE", "COMBO_CAPTIONOFFSET", "COMBO_CAPTION_SHADOW_ENABLED",
        "COMBO_CAPTION_SHADOW_X", "COMBO_CAPTION_SHADOW_Y", "COMBO_CAPTION_SHADOW_SOFTNESS", "COMBO_CAPTION_SHADOW_COLOR", "COMBO_COUNT_THICKNESS",
        "COMBO_COUNT_SHADOW_ENABLED", "COMBO_COUNT_SHADOW_X", "COMBO_COUNT_SHADOW_Y", "COMBO_COUNT_SHADOW_SOFTNESS", "COMBO_COUNT_SHADOW_COLOR",
        "COMBO_NOPOP", "COMBO_PULSE_DURATION", "COMBO_PULSE_COUNT_SCALE", "COMBO_PULSE_LABEL_OFFSET", "COMBO_COLORMAX", "COMBO_SOLIDCOLOR",
        "COMBO_PERFECTCOLOR_ENABLED", "COMBO_COLORLOW", "COMBO_COLORHIGH", "COMBO_PERFECTCOLOR", "COMBO_RESETPOS",
    }.Where(k => k != "COMBO_XPERFECT" || XPerfectBridge.Installed)];

    private static IEnumerator OverlayCombo(Action<string, bool, string> check, Action<string> info) {
        bool wasOpen = UICore.IsOpen;
        ComboOverlay.EnsureConf();
        // Every feature is read before any is flipped, and put back in reverse: "overlay" is Combo's dependency.
        var saved = FeatureService.Features.Select(h => (id: h.Id, was: h.Enabled, had: FeatureService.State.Features.TryGetValue(h.Id, out bool v), value: v)).ToList();
        ComboSettings c = ComboConf;
        var shown = (c.ShowCaption, c.CaptionShadowEnabled, c.CountShadowEnabled, c.NoPopAnim, c.SolidColor, c.PerfectColorEnabled);
        float size = c.FontSize;
        if(wasOpen) UICore.Close(true);
        HandOff.ReturnTo = HandOff.Menu.None;
        try {
            foreach(string id in new[] { "overlay", "combo" }) if(!FeatureService.IsEnabled(id)) FeatureService.SetEnabled(id, true);
            yield return Frames(3);
            // Every row a switch hides shows for the parity check.
            (c.ShowCaption, c.CaptionShadowEnabled, c.CountShadowEnabled, c.NoPopAnim, c.SolidColor, c.PerfectColorEnabled) = (true, true, true, false, false, true);
            ComboOverlay.Apply();
            FacetBridge.OpenNewMenu(ComboPage);
            yield return Frames(3);
            Parity(check, ComboPage, ComboRows());
            yield return ComboEdges(check);
            yield return ClassicSees(check, ComboPage, "COMBO_CAPTION", Bind.To(() => ComboConf.ShowCaption, v => {
                ComboConf.ShowCaption = v;
                ComboOverlay.Apply();
                ComboOverlay.Save();
            }));
            yield return Hotkey(check, ComboPage, "COMBO_NOPOP", "combo_nopop", null, Bind.To(() => ComboConf.NoPopAnim, v => {
                ComboConf.NoPopAnim = v;
                ComboOverlay.Save();
            }));
            yield return ComboFontKeepsPreview(check);
            yield return ComboFontsFollow(check);
            yield return ShadowResets(check);
            yield return CaptionCaps(check);
            // Through the row's own slider: font size up 10 (down where that would pass the top), read off the counter.
            float want = size + 10f <= 120f ? size + 10f : size - 10f;
            yield return PreviewChecks(check, info, ComboPage, ComboOverlay.FontKey,
                () => {
                    if(QuartzShell.Surface?.Root.Q(name: "COMBO_FONTSIZE-input") is not Slider s) return false;
                    s.value = (want - 24f) / (120f - 24f);   // the track runs 0 to 1 over the row's range
                    return true;
                },
                () => OverlayPreview.ShownOf(ComboOverlay.FontKey)?.transform.Find("Value")?.GetComponent<TextMeshProUGUI>() is { } value
                    && Mathf.Abs(value.fontSize - want) < 0.5f);
        } finally {
            c = ComboConf;
            (c.ShowCaption, c.CaptionShadowEnabled, c.CountShadowEnabled, c.NoPopAnim, c.SolidColor, c.PerfectColorEnabled) = shown;
            c.FontSize = size;
            ComboOverlay.Apply();
            ComboOverlay.Save();
            HandOff.ReturnTo = HandOff.Menu.None;
            QuartzShell.Hide();
            if(UICore.IsOpen) UICore.Close(true);
            for(int i = saved.Count - 1; i >= 0; i--) RestoreFeature(saved[i].id, saved[i].was, saved[i].had, saved[i].value);
            if(wasOpen) UICore.Open(true);
        }
        yield return Frames(3);
    }

    // The two columns: side by side, the page's parts share the header's edges, the settings cards their own and the preview
    // ends on the header's right edge. The same once the window is narrowed until the columns stack.
    private static IEnumerator ComboEdges(Action<string, bool, string> check) {
        VisualElement page = PageContent(), right = page.Q(name: ComboOverlay.FontKey + "-preview-pane")?.parent, left = right?.parent?[0];
        if(right == null || left == null || left == right) {
            check($"{ComboPage}: the settings and the preview sit in two columns", false, $"pane={right != null}");
            yield break;
        }
        bool Stacked() => right.worldBound.yMin >= left.worldBound.yMax - 1f;
        bool side = !Stacked();
        string misfits = EdgeMisfits(page, left, right);
        check($"{ComboPage}: side by side, the page's parts share the header's edges, the cards their own, and the preview ends on the header's right (±1)",
            side && misfits == "", $"side={side} {misfits}");
        Rect was = QuartzShell.Window.Rect;
        try {
            // The window's layout width is its rect over its scale; the columns stack below 760 of theirs.
            float columns = right.parent.layout.width, cut = (columns - 700f) * QuartzShell.Window.Scale;
            QuartzShell.Window.Rect = new Rect(was.x, was.y, was.width - cut, was.height);
            int frames = 0;
            for(; frames < 10 && !Stacked(); frames++) yield return null;
            yield return Frames(2);
            misfits = EdgeMisfits(page, left, right);
            check($"{ComboPage}: narrowed below 760, the preview stacks under the cards and the edges still hold (±1)", Stacked() && misfits == "",
                $"stacked={Stacked()} frames={frames} width={right.parent.layout.width:0} {misfits}");
        } finally {
            QuartzShell.Window.Rect = was;
        }
        yield return Frames(2);
    }

    // A font picked for Combo through its row changes the counter's face only: the page is not rebuilt, so the preview keeps
    // the lease it holds.
    private static IEnumerator ComboFontKeepsPreview(Action<string, bool, string> check) {
        string key = ComboOverlay.FontKey, was = FontManager.OverlayFontName(key) ?? FontManager.FollowGlobal;
        List<string> fonts = [.. OverlayFontPicker.Values().Where(f => f != FontManager.FollowGlobal && f != was)];
        int frames = 0;
        for(; frames < 10 && OverlayPreview.Current == null; frames++) yield return null;
        OverlayPreview.Lease lease = OverlayPreview.Current;
        VisualElement row = Body.Q(name: "FONT_OVERLAY-input");
        if(fonts.Count == 0 || lease == null || row == null) {
            check($"{ComboPage}: a font picked in the row keeps the page and its preview", false, $"fonts={fonts.Count} lease={lease != null} row={row != null}");
            yield break;
        }
        try {
            yield return PickItem("FONT_OVERLAY", OverlayFontPicker.Display(fonts[0]));
            bool picked = FontManager.OverlayFontName(key) == fonts[0];
            check($"{ComboPage}: a font picked in the row is Combo's, the page is not rebuilt and the preview keeps the same live lease",
                picked && ReferenceEquals(Body.Q(name: "FONT_OVERLAY-input"), row) && ReferenceEquals(OverlayPreview.Current, lease) && !lease.Released,
                $"picked={FontManager.OverlayFontName(key)} want={fonts[0]} row={ReferenceEquals(Body.Q(name: "FONT_OVERLAY-input"), row)} "
                + $"lease={ReferenceEquals(OverlayPreview.Current, lease)} released={lease.Released}");
        } finally {
            FontManager.SetOverlayFont(key, was);
        }
        yield return Frames(2);
    }

    // A global font chosen, or a font found, with the page open rebuilds the font row: item 0 names the new global font, and the
    // list holds the new face. Nothing is saved: the global font is set unsaved and put back, the found font is never on disk.
    private static IEnumerator ComboFontsFollow(Action<string, bool, string> check) {
        string global = FontManager.CurrentName, other = global == FontManager.DefaultName ? FontManager.GameFontName : FontManager.DefaultName;
        const string found = "self-test font";
        FieldInfo cache = typeof(FontManager).GetField("available", BindingFlags.Static | BindingFlags.NonPublic);
        // The shell's own handler on the catalog event, raised alone: the classic pages' handlers are not this check's.
        Action shell = (typeof(FontManager).GetField(nameof(FontManager.OnFontCatalogChanged), BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as Action)
            ?.GetInvocationList().OfType<Action>().FirstOrDefault(a => a.Method.DeclaringType == typeof(QuartzShell));
        VisualElement row = Body.Q(name: "FONT_OVERLAY-input");
        bool listed = false;
        try {
            FontManager.SetFont(other, false);
            int frames = 0;
            for(; frames < 5 && ReferenceEquals(Body.Q(name: "FONT_OVERLAY-input"), row); frames++) yield return null;
            string item = OverlayFontPicker.Display(FontManager.FollowGlobal);
            yield return FontMenuLists(item, v => listed = v);
            check($"{ComboPage}: a new global font rebuilds the font row within 5 frames, and its first item names that font",
                FontManager.CurrentName == other && row != null && frames < 5 && listed,
                $"global={FontManager.CurrentName} want={other} frames={frames} item='{item}' listed={listed}");
            row = Body.Q(name: "FONT_OVERLAY-input");
            string[] more = [.. FontManager.GetAvailableFonts(), found];
            cache?.SetValue(null, more);
            shell?.Invoke();
            frames = 0;
            for(; frames < 5 && ReferenceEquals(Body.Q(name: "FONT_OVERLAY-input"), row); frames++) yield return null;
            listed = false;
            yield return FontMenuLists(found, v => listed = v);
            check($"{ComboPage}: a font found rebuilds the font row within 5 frames, and the row lists it",
                cache != null && shell != null && row != null && frames < 5 && listed, $"cache={cache != null} hooked={shell != null} frames={frames} listed={listed}");
        } finally {
            Popup.CloseAll();
            cache?.SetValue(null, null);   // read afresh from the scanned files
            FontManager.SetFont(global, false);
            shell?.Invoke();
        }
        yield return Frames(3);
    }

    // Opens the font row's menu and says whether it lists the item; the menu is shut again.
    private static IEnumerator FontMenuLists(string item, Action<bool> listed) {
        Button b = Body.Q<Button>("FONT_OVERLAY-input");
        if(b == null) yield break;
        ContentScroll.ScrollTo(b);
        yield return Frames(2);
        Submit(b);
        yield return Frames(2);
        listed(MenuRow(item) != null);
        Popup.CloseAll();
        yield return Frames(1);
    }

    // The caption row holds 24 characters, as the classic field does: 30 committed in the field store 24, and the field, which
    // stops typing at 24, shows 24 at once.
    private static IEnumerator CaptionCaps(Action<string, bool, string> check) {
        string was = ComboConf.CaptionText, typed = new('x', 30);
        TextField field = Body.Q<TextField>("COMBO_CAPTIONTEXT-input");
        try {
            if(field != null) {
                field.value = typed;
                using KeyDownEvent ev = Pooled<KeyDownEvent>();   // Enter commits it
                typeof(KeyboardEventBase<KeyDownEvent>).GetProperty("keyCode").SetValue(ev, KeyCode.Return);
                ev.target = field;
                field.SendEvent(ev);
            }
            yield return Frames(1);
            check($"{ComboPage}: 30 characters written in the caption row store 24, and the field shows 24",
                field != null && field.maxLength == 24 && ComboConf.CaptionText == typed[..24] && field.text == typed[..24],
                $"field={field != null} cap={field?.maxLength} stored={ComboConf.CaptionText?.Length} shown={field?.text?.Length}");
        } finally {
            ComboConf.CaptionText = was;
            ComboOverlay.Apply();
            ComboOverlay.Save();
        }
    }

    // A middle-click on a shadow row puts back the classic default, after the row's own slider moved it off.
    private static IEnumerator ShadowResets(Action<string, bool, string> check) {
        var def = new ComboSettings();
        float x = ComboConf.CaptionShadowX, off = def.CaptionShadowX + 2f;
        VisualElement row = Body.Q(name: "COMBO_CAPTION_SHADOW_X");
        try {
            if(row?.Q<Slider>() is { } s) s.value = (off + 10f) / 20f;   // the track runs 0 to 1 over the row's -10 to 10
            yield return Frames(1);
            bool moved = Mathf.Abs(ComboConf.CaptionShadowX - off) < 0.01f;
            if(row != null) Press(row, 2);
            yield return Frames(1);
            check($"{ComboPage}: a middle-click on a shadow row puts back its classic default",
                moved && Mathf.Abs(ComboConf.CaptionShadowX - def.CaptionShadowX) < 0.01f,
                $"row={row != null} moved={moved} x={ComboConf.CaptionShadowX} want={def.CaptionShadowX}");
        } finally {
            ComboConf.CaptionShadowX = x;
            ComboOverlay.ApplyCaptionShadow();
            ComboOverlay.Save();
        }
    }
}
#endif

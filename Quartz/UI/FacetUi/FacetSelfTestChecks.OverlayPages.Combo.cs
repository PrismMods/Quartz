#if DEBUG && !IL2CPP
using System.Collections;
using System.Reflection;
using Facet;
using Quartz.Features.Combo;
using Quartz.Features.Interop;
using Quartz.Overlay;
using Quartz.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Overlay ▸ Combo in the new menu: its rows against the classic page, its two columns' edges side by side and stacked, the
/// caption switch seen by the classic page, the no-pop hotkey, a font pick that keeps the preview, a new global or found
/// font reaching the font row, a shadow row's middle-click, and the preview pane.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string ComboPage = "overlay.combo";
    private static ComboSettings ComboConf => ComboOverlay.Conf;

    // Every row of the classic page, from Auto to Reset Position; the XPerfect row only where that mod is installed.
    private static string[] ComboRows() => [.. new[] {
        "COMBO_AUTO", "COMBO_XPERFECT", "COMBO_ANCHOR_LABEL", "FONT_OVERLAY", "COMBO_FONTSIZE",
        "COMBO_MASTER_SIZE", "COMBO_CAPTION", "COMBO_CAPTIONTEXT", "COMBO_CAPTION_SIZE", "COMBO_CAPTIONOFFSET", "COMBO_CAPTION_SHADOW_ENABLED",
        "COMBO_CAPTION_SHADOW_X", "COMBO_CAPTION_SHADOW_Y", "COMBO_CAPTION_SHADOW_SOFTNESS", "COMBO_CAPTION_SHADOW_COLOR", "COMBO_COUNT_THICKNESS",
        "COMBO_COUNT_SHADOW_ENABLED", "COMBO_COUNT_SHADOW_X", "COMBO_COUNT_SHADOW_Y", "COMBO_COUNT_SHADOW_SOFTNESS", "COMBO_COUNT_SHADOW_COLOR",
        "COMBO_NOPOP", "COMBO_PULSE_DURATION", "COMBO_PULSE_COUNT_SCALE", "COMBO_PULSE_LABEL_OFFSET", "COMBO_COLORMAX", "COMBO_SOLIDCOLOR",
        "COMBO_PERFECTCOLOR_ENABLED", "COMBO_COLORLOW", "COMBO_COLORHIGH", "COMBO_PERFECTCOLOR", "COMBO_RESETPOS",
    }.Where(k => k != "COMBO_XPERFECT" || XPerfectBridge.Installed)];

    private static IEnumerator OverlayCombo(Action<string, bool, string> check, Action<string> info) {
        ComboOverlay.EnsureConf();
        ComboSettings c = ComboConf;
        var shown = (c.ShowCaption, c.CaptionShadowEnabled, c.CountShadowEnabled, c.NoPopAnim, c.SolidColor, c.PerfectColorEnabled, c.CustomBreak);
        float size = c.FontSize;
        yield return OnPage("combo", ComboPage, () => {
            // Every row a switch hides shows for the parity check; the XPerfect row hides under Custom Break.
            (c.ShowCaption, c.CaptionShadowEnabled, c.CountShadowEnabled, c.NoPopAnim, c.SolidColor, c.PerfectColorEnabled, c.CustomBreak) = (true, true, true, false, false, true, false);
            ComboOverlay.Apply();
        }, () => {
            c = ComboConf;
            (c.ShowCaption, c.CaptionShadowEnabled, c.CountShadowEnabled, c.NoPopAnim, c.SolidColor, c.PerfectColorEnabled, c.CustomBreak) = shown;
            c.FontSize = size;
            ComboOverlay.Apply();
            ComboOverlay.Save();
        }, () => ComboChecks(check, info, size));
    }

    private static IEnumerator ComboChecks(Action<string, bool, string> check, Action<string> info, float size) {
        // The groups sit on tabs, one built at a time: each check shows its row's tab first.
        yield return ShowTab("SECTION_COMBO");
        yield return Parity(check, ComboPage, ComboRows());
        yield return ComboTabs(check);
        yield return ShowTab("SECTION_COMBO");
        yield return ColumnEdges(check, ComboPage, ComboOverlay.FontKey);
        HeaderSwitchEdge(check, ComboPage, "SECTION_COMBO", "COMBO_AUTO");
        yield return ShowTab("HEADING_LABEL");
        yield return OsCursor(check, info, Body.Q(name: "COMBO_CAPTIONTEXT-input"), "Combo's caption text field");
        yield return Flips(check, ComboPage, "COMBO_CAPTION", Bind.To(() => ComboConf.ShowCaption, v => {
            ComboConf.ShowCaption = v;
            ComboOverlay.Apply();
            ComboOverlay.Save();
        }));
        yield return ShowTab("HEADING_ANIMATION");
        yield return Hotkey(check, ComboPage, "COMBO_NOPOP", "combo_nopop", null, Bind.To(() => ComboConf.NoPopAnim, v => {
            ComboConf.NoPopAnim = v;
            ComboOverlay.Save();
        }));
        yield return ShowTab("SECTION_COMBO");
        yield return ComboFontKeepsPreview(check);
        yield return ComboFontsFollow(check);
        yield return ComboFontFaces(check);
        yield return ShowTab("HEADING_LABEL");
        yield return ShadowResets(check);
        yield return CaptionCaps(check);
        yield return ShowTab("SECTION_COMBO");
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
    }

    // One tab's rows show at a time: the first tab's font size row, not the colour tab's Color Max. A search for Color Max
    // opens its tab and shows the row.
    private static IEnumerator ComboTabs(Action<string, bool, string> check) {
        const string name = $"{ComboPage}: one tab's rows show at a time, and a search for a row on another tab opens it";
        yield return ShowTab("SECTION_COMBO");
        global::Facet.Tabs tabs = PageTabs();
        bool one = tabs != null && Body.Q(name: "COMBO_FONTSIZE-input") is { } size && Displayed(size) && Body.Q(name: "COMBO_COLORMAX") == null;
        List<SearchIndex.Hit> hits = [];
        int at = -1;
        try {
            hits = Find(QuartzHost.Host.Translate("COMBO_COLORMAX"));
            at = hits.FindIndex(h => h.PageRef != null && Unnumbered(h.Key) == "COMBO_COLORMAX");
            if(at >= 0) Click(Body[at]);
            bool shown = false;
            for(int i = 0; i < 30 && at >= 0 && !shown; i++) {
                yield return null;
                shown = Body.Q(name: "COMBO_COLORMAX") is { } row && Displayed(row);
            }
            tabs = PageTabs();
            check(name, one && at >= 0 && shown && QuartzShell.CurrentPage == ComboPage && tabs?.SelectedName == "HEADING_COLOR",
                $"one={one} hit={at} shown={shown} page={QuartzShell.CurrentPage} tab={tabs?.SelectedName} {(at < 0 ? Hits(hits) : "")}");
        } finally {
            QuartzShell.Nav?.Filter("");
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
            yield return FontMenuRow(item, r => listed = r != null);
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
            yield return FontMenuRow(found, r => listed = r != null);
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

    // The font row's menu draws a font in its own face, and the follow-the-global item in the global font's face, as the classic
    // picker does. A font other than the global one is picked where there is one, so a menu drawing every item in one face fails.
    private static IEnumerator ComboFontFaces(Action<string, bool, string> check) {
        var fonts = OverlayFontPicker.Values().Where(f => f != FontManager.FollowGlobal && FontManager.GetFont(f)?.sourceFontFile != null).ToList();
        string font = fonts.FirstOrDefault(f => FontManager.GetFont(f) != FontManager.Current) ?? fonts.FirstOrDefault();
        UnityEngine.TextCore.Text.FontAsset want = font == null ? null : ShellSettings.Face(FontManager.GetFont(font)), wantGlobal = ShellSettings.Face(FontManager.Current);
        UnityEngine.TextCore.Text.FontAsset face = null, global = null;
        if(font != null) yield return FontMenuRow(OverlayFontPicker.Display(font), r => face = r?.Q<Label>()?.style.unityFontDefinition.value.fontAsset);
        yield return FontMenuRow(OverlayFontPicker.Display(FontManager.FollowGlobal), r => global = r?.Q<Label>()?.style.unityFontDefinition.value.fontAsset);
        check($"{ComboPage}: the font row's menu draws a font in its own face, and the global item in the global font's face",
            want != null && ReferenceEquals(face, want) && ReferenceEquals(global, wantGlobal),
            $"font={font} own={ReferenceEquals(face, want)} other={want != wantGlobal} global={ReferenceEquals(global, wantGlobal)} globalFace={wantGlobal != null}");
    }

    // Opens the font row's menu and hands over the item's menu row (null when it is not listed); the menu is shut again.
    private static IEnumerator FontMenuRow(string item, Action<VisualElement> found) {
        Button b = Body.Q<Button>("FONT_OVERLAY-input");
        if(b == null) yield break;
        ContentScroll.ScrollTo(b);
        yield return Frames(2);
        Submit(b);
        yield return Frames(2);
        found(MenuRow(item));
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

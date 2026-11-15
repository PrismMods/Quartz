#if !IL2CPP
using Facet;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Resource;
using TMPro;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Settings' Font and Settings Window Font cards, row for row as the classic page's sections. Adding a font is a row of its
/// own under the list: a dropdown item cannot start a file dialog. The open page rebuilds when the font or the catalog
/// changes, so the lists are read at build.
/// </summary>
internal static partial class ShellSettings {
    // The rename field's text, for the font it was typed for; another font starts from its own name.
    private static (string Font, string Name) pendingName;
    // The last font message and its tone; a success rebuilds the page, so it outlives the card that showed it.
    private static (string Text, Role Tone) fontMessage = ("", Role.TextMuted);
    // Each font's face for its list item, made once per TMP font.
    private static readonly Dictionary<TMP_FontAsset, FontAsset> Faces = [];

    internal static void FontCards(VisualElement p) {
        DropGoneFaces();
        VisualElement card = Card(p, "FONT");
        StatusLine status = null;
        Controls.Dropdown(card, "", QuartzShell.Writes(() => FontManager.CurrentName, name => {
            Say(status, "", Role.TextMuted);
            FontManager.SetFont(name, true);
        }), [.. FontManager.GetAvailableFonts()], FontText, o => {
            o.Id = "FONT_DROPDOWN";
            o.ItemFace = name => Face(FontManager.GetFont(name));
        });
        Controls.Action(card, "", "FONT_ADD", () => AddFont(status), o => o.Id = "FONT_ADD");
        AdjustSliders(card, () => FontManager.CurrentName, "", null);
        Func<bool> custom = () => FontManager.IsCustomFont(FontManager.CurrentName);
        ShellRows.Text(card, "FONT_NAME", Bind.To(() => pendingName.Font == FontManager.CurrentName ? pendingName.Name : FontManager.CurrentName,
            v => pendingName = (FontManager.CurrentName, v)), 40, null, null, o => {
            o.Undoable = false;
            o.VisibleWhen = custom;
        });
        Controls.Action(card, "", "FONT_RENAME", () => Rename(status), o => {
            o.Id = "FONT_RENAME";
            o.VisibleWhen = custom;
        });
        // Danger is the classic two-click "Sure?" already.
        Controls.Danger(card, "", "FONT_DELETE", () => Delete(status), o => {
            o.Id = "FONT_DELETE";
            o.VisibleWhen = custom;
        });
        status = Controls.Status(card);
        if(fontMessage.Text.Length > 0) Say(status, fontMessage.Text, fontMessage.Tone);

        VisualElement window = Card(p, "SETTINGS_FONT");
        Controls.Dropdown(window, "", QuartzShell.Writes(() => string.IsNullOrEmpty(C.SettingsFontName) ? FontManager.UiDefault : C.SettingsFontName, name => {
            C.SettingsFontName = name == FontManager.UiDefault ? "" : name;
            Save();
            FontManager.ApplyMenuFont();
        }), [FontManager.UiDefault, FontManager.SameAsOverlay, .. FontManager.GetAvailableFonts()], WindowFontText, o => {
            o.Id = "SETTINGS_FONT_DROPDOWN";
            o.Tooltip = "DESC_SETTINGS_FONT";
            o.ItemFace = name => Face(name == FontManager.UiDefault ? FontManager.UiFont : FontManager.GetFont(name));
        });
        AdjustSliders(window, WindowAdjustTarget, "#2", () => WindowAdjustTarget() != null);
    }

    // Size and line spacing for target's font, live as they move and saved on release; hidden while target is null.
    private static void AdjustSliders(VisualElement card, Func<string> target, string suffix, Func<bool> visible) {
        ShellRows.ReleaseSlider(card, "FONT_SCALE", Bind.To(() => FontManager.GetAdjust(target()).Scale,
            v => FontManager.SetAdjust(target(), v, FontManager.GetAdjust(target()).LineSpacing)),
            FontManager.MinAdjust, FontManager.MaxAdjust, 0.01f, "0.00 x", null, Save, o => Adjust(o, "FONT_SCALE" + suffix, "DESC_FONT_SCALE", visible));
        ShellRows.ReleaseSlider(card, "FONT_LINE_SPACING", Bind.To(() => FontManager.GetAdjust(target()).LineSpacing,
            v => FontManager.SetAdjust(target(), FontManager.GetAdjust(target()).Scale, v)),
            FontManager.MinAdjust, FontManager.MaxAdjust, 0.01f, "0.00 x", null, Save, o => Adjust(o, "FONT_LINE_SPACING" + suffix, "DESC_FONT_LINE_SPACING", visible));
    }

    private static void Adjust(SliderOptions o, string id, string tooltip, Func<bool> visible) {
        o.Id = id;
        o.Tooltip = tooltip;
        o.Default = 1f;
        o.VisibleWhen = visible;
    }

    // "Same as overlay" already follows the Font card's sliders, so it has no pair of its own.
    private static string WindowAdjustTarget() =>
        string.IsNullOrEmpty(C.SettingsFontName) ? FontManager.UiDefault : C.SettingsFontName == FontManager.SameAsOverlay ? null : C.SettingsFontName;

    private static string FontText(string name) =>
        name == FontManager.DefaultName ? string.Format(QuartzHost.Translate("FONT_DEFAULT_OF"), FontManager.DefaultFontFile) : name;

    private static string WindowFontText(string name) =>
        name == FontManager.UiDefault ? "FONT_UI_DEFAULT" : name == FontManager.SameAsOverlay ? "FONT_SAME_AS_OVERLAY" : FontText(name);

    // null, the menu's own face, when the font has no source file to make one from.
    private static FontAsset Face(TMP_FontAsset font) {
        if(font == null || font.sourceFontFile == null) return null;
        if(!Faces.TryGetValue(font, out FontAsset face)) Faces[font] = face = FontAsset.CreateFontAsset(font.sourceFontFile);
        return face;
    }

    // A renamed or deleted font's TMP asset is destroyed by FontManager: its face (and with it the face's atlas) goes at the
    // next build. The page rebuilds on the catalog change, a frame later at most.
    private static void DropGoneFaces() {
        foreach(TMP_FontAsset gone in Faces.Keys.Where(k => k == null).ToList()) {
            UnityEngine.Object.Destroy(Faces[gone]);
            Faces.Remove(gone);
        }
    }

    private static void Say(StatusLine status, string text, Role tone) {
        fontMessage = (text, tone);
        if(text.Length == 0) status?.Clear();
        else status?.Show(() => text, tone);
    }

    private static void AddFont(StatusLine status) {
        string path;
        try {
            path = FileDialog.PickFile(null, "Font", ["ttf", "otf", "ttc"], QuartzHost.Translate("FONT_PICK_TITLE"));
        } catch(Exception e) {
            MainCore.Log.Err($"[ShellSettings] font PickFile failed: {e}");
            return;
        }
        if(string.IsNullOrEmpty(path)) return;
        string name = FontManager.ImportFont(path);
        if(name == null) {
            Say(status, QuartzHost.Translate("FONT_IMPORT_FAILED"), Role.Danger);
            return;
        }
        FontManager.SetFont(name, true);
        Say(status, string.Format(QuartzHost.Translate("FONT_ADDED"), name), Role.TextMuted);
    }

    private static void Rename(StatusLine status) {
        string cur = FontManager.CurrentName, name = pendingName.Font == cur ? pendingName.Name : cur;
        if(!FontManager.IsCustomFont(cur)) return;
        if(FontManager.RenameFont(cur, name, out string error)) Say(status, string.Format(QuartzHost.Translate("FONT_RENAMED"), FontManager.CurrentName), Role.TextMuted);
        else Say(status, error, Role.Danger);
    }

    private static void Delete(StatusLine status) {
        string cur = FontManager.CurrentName;
        if(FontManager.IsCustomFont(cur) && FontManager.DeleteFont(cur)) Say(status, string.Format(QuartzHost.Translate("FONT_DELETED"), cur), Role.TextMuted);
    }
}
#endif

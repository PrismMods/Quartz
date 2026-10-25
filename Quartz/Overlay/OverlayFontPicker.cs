using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using UnityEngine;
namespace Quartz.Overlay;
// Font row for an overlay page: the overlay's own font, or the global one from Settings.
public static class OverlayFontPicker {
    public static UIDropDown<string> Dropdown(Transform parent, string overlayKey, string id) {
        UIDropDown<string> dropdown = GenerateUI.DropDown(
            GenerateUI.Row(parent),
            FontManager.FollowGlobal,
            Current(overlayKey),
            Values(),
            Display,
            v => FontManager.SetOverlayFont(overlayKey, v),
            id,
            260f,
            "Font"
        );
        dropdown.HeaderDisplay = v => $"{GenerateUI.Tr("FONT_OVERLAY", "Font")}: {Display(v)}";
        dropdown.ItemFont = n => n == FontManager.FollowGlobal ? FontManager.Current : FontManager.GetFont(n);
        void Refresh() {
            if(dropdown.IsDisposed || dropdown.Rect == null) {
                Unhook();
                return;
            }
            dropdown.SetValues(Values());
            dropdown.Set(Current(overlayKey), false);
            dropdown.RefreshLanguage();
        }
        void Unhook() {
            FontManager.OnFontCatalogChanged -= Refresh;
            FontManager.OnFontChanged -= Refresh;
        }
        FontManager.OnFontCatalogChanged += Refresh;
        FontManager.OnFontChanged += Refresh;
        dropdown.OnDisposed += Unhook;
        return dropdown;
    }
    private static string Current(string overlayKey) => FontManager.OverlayFontName(overlayKey) ?? FontManager.FollowGlobal;
    private static IReadOnlyList<string> Values() => [FontManager.FollowGlobal, .. FontManager.GetAvailableFonts()];
    private static string Display(string name) =>
        name == FontManager.FollowGlobal
            ? string.Format(GenerateUI.Tr("FONT_OVERLAY_GLOBAL", "Global font ({0})"), FontName(FontManager.CurrentName))
            : FontName(name);
    private static string FontName(string name) =>
        name == FontManager.DefaultName
            ? string.Format(GenerateUI.Tr("FONT_DEFAULT_OF", "Default ({0})"), FontManager.DefaultFontFile)
            : name;
}

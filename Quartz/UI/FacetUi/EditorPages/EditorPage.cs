#if !IL2CPP
using Facet;
using Quartz.Features.Editor;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.EditorPages;
/// <summary>The four Editor pages in the new menu, row for row as PageEditor. They share the editor feature, so each
/// page's header switch is the same one.</summary>
internal static class EditorPage {
    private static EditorSettings C => EditorFeature.Conf;
    private static readonly EditorSettings Def = new();

    internal static void TileReadout(VisualElement p) => Page(p, "SECTION_SELECTED_TILE_READOUT", "EDITOR_TILEREADOUT_DESC", (card, _) => {
        Row(card, "EDITOR_SHOW_FLOOR_ANGLE", () => C.ShowFloorAngle, v => C.ShowFloorAngle = v, Def.ShowFloorAngle, EditorFeature.Apply);
        Row(card, "EDITOR_SHOW_FLOOR_BEATS", () => C.ShowFloorBeats, v => C.ShowFloorBeats = v, Def.ShowFloorBeats, EditorFeature.Apply);
        Row(card, "EDITOR_SHOW_FLOOR_COUNT", () => C.ShowFloorCount, v => C.ShowFloorCount = v, Def.ShowFloorCount, EditorFeature.Apply);
        Row(card, "EDITOR_SHOW_FLOOR_DURATION", () => C.ShowFloorDuration, v => C.ShowFloorDuration = v, Def.ShowFloorDuration, EditorFeature.Apply);
        Row(card, "EDITOR_USE_TULTTAK_BEHAVIOR", () => C.UseTulttakModBehavior, v => C.UseTulttakModBehavior = v, Def.UseTulttakModBehavior,
            EditorFeature.Apply);
    });

    internal static void Bga(VisualElement p) => Page(p, "SECTION_BGA_MOD", "EDITOR_BGA_DESC", (card, _) => {
        Row(card, "EDITOR_BGA_MOD", () => C.BgaMod, v => C.BgaMod = v, Def.BgaMod, EditorFeature.Apply);
        Row(card, "EDITOR_BGA_HIDE_TILE_DECO", () => C.BgaHideTileDeco, v => C.BgaHideTileDeco = v, Def.BgaHideTileDeco, EditorFeature.Apply);
        Row(card, "EDITOR_BGA_HIDE_PLANET_DECO", () => C.BgaHidePlanetDeco, v => C.BgaHidePlanetDeco = v, Def.BgaHidePlanetDeco, EditorFeature.Apply);
    });

    internal static void FlipRotate(VisualElement p) => Page(p, "SECTION_FLIP_ROTATE_TILES", "EDITOR_FLIPROTATE_DESC", (card, cards) => {
        Row(card, "EDITOR_FLIP_ADJUST", () => C.AdjustOnFlip, v => C.AdjustOnFlip = v, Def.AdjustOnFlip, null);
        Row(card, "EDITOR_ROTATE_ADJUST", () => C.AdjustOnRotate, v => C.AdjustOnRotate = v, Def.AdjustOnRotate, null);
        Row(card, "EDITOR_CUSTOM_ANGLE", () => C.CustomAngleRotation, v => C.CustomAngleRotation = v, Def.CustomAngleRotation, null);
        ShellRows.ReleaseSlider(card, "EDITOR_CUSTOM_ANGLE_VALUE", Bind.To(() => C.CustomAngle, v => C.CustomAngle = v), 1f, 359f, 0.001f, "0.###°",
            null, EditorFeature.Save, o => o.Default = Def.CustomAngle);
        VisualElement keys = Controls.Card(cards, "HEADING_SHORTCUTS", o => o.Id = "HEADING_SHORTCUTS");
        foreach(string key in new[] { "EDITOR_FLIPROTATE_SHORTCUT_FLIP_H", "EDITOR_FLIPROTATE_SHORTCUT_FLIP_V", "EDITOR_FLIPROTATE_SHORTCUT_ROT_CCW",
            "EDITOR_FLIPROTATE_SHORTCUT_ROT_CW", "EDITOR_FLIPROTATE_SHORTCUT_ROT_180" })
            Controls.Info(keys, key, () => "", (InfoOptions o) => o.Id = key);
    });

    internal static void DecoPreview(VisualElement p) => Page(p, "SECTION_DECORATION_PREVIEW", "EDITOR_DECOPREVIEW_DESC", (card, _) => {
        Row(card, "EDITOR_DECO_PREVIEW", () => C.DecoPreview, v => C.DecoPreview = v, Def.DecoPreview, EditorFeature.RefreshDecoPreview);
        Controls.Info(card, "EDITOR_DECO_PREVIEW_CREDIT", () => "", (InfoOptions o) => o.Id = "EDITOR_DECO_PREVIEW_CREDIT");
    });

    // rows gets the page's first card and the element the cards go in.
    private static void Page(VisualElement p, string titleKey, string descKey, Action<VisualElement, VisualElement> rows) {
        EditorFeature.EnsureConf();
        ShellPage.Feature(p, "editor", titleKey, descKey, null, cards => rows(Controls.Card(cards, null), cards));
    }

    // The classic id is the key in lower case; the tooltip is DESC_<key>.
    private static void Row(VisualElement card, string key, Func<bool> get, Action<bool> set, bool def, Action apply) =>
        ShellRows.Toggle(card, key, key.ToLowerInvariant(), Bind.To(get, set), apply, EditorFeature.Save, o => {
            o.Default = def;
            o.Tooltip = "DESC_" + key;
        });
}
#endif

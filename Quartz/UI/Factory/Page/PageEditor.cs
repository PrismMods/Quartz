using Quartz.Features.Editor;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;

namespace Quartz.UI.Factory.Page;

// Editor tab. Hosts tweaks that target A Dance of Fire and Ice's level editor.
// Each section is its own standalone page under the Editor category's column-2
// (see MenuFactory.CategoryChildren). All rows share the same callback shape
// (write the setting, re-apply the feature, save), re-declared per page.
internal static partial class PageEditor {
    // === Inspector ===
    public static void InspectorPage(RectTransform parent) {
        EditorFeature.EnsureConf();
        EditorSettings conf = EditorFeature.Conf;
        EditorSettings def = new();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);

        void Toggle(Transform body, bool dft, bool val, Action<bool> set, string label, string id, string desc) =>
            GenerateUI.ToggleTip(body, dft, val, v => { set(v); EditorFeature.Apply(); EditorFeature.Save(); }, label, id, desc);

        var inspectorSec = GenerateUI.FlatSection(content.transform, "Inspector");

        Toggle(inspectorSec.Body, def.HorizontalProperties, conf.HorizontalProperties,
            v => conf.HorizontalProperties = v,
            "Horizontal Properties", "editor_horizontal_properties",
            "Lays each level-editor inspector property out as \"label [field]\" on one row, instead of the label stacked above the field. Affects the in-game editor, not this settings window.");
    }

    // === Selected-tile readout (ported from AdofaiTweaks) ===
    public static void TileReadoutPage(RectTransform parent) {
        EditorFeature.EnsureConf();
        EditorSettings conf = EditorFeature.Conf;
        EditorSettings def = new();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);

        void Toggle(Transform body, bool dft, bool val, Action<bool> set, string label, string id, string desc) =>
            GenerateUI.ToggleTip(body, dft, val, v => { set(v); EditorFeature.Apply(); EditorFeature.Save(); }, label, id, desc);

        var tileReadoutSec = GenerateUI.FlatSection(content.transform, "Selected Tile Readout");

        Toggle(tileReadoutSec.Body, def.ShowFloorAngle, conf.ShowFloorAngle,
            v => conf.ShowFloorAngle = v,
            "Show selected tiles' angle", "editor_show_floor_angle",
            "Shows the total angle (in degrees) of the selected tiles, on one of the tiles in the level editor. Affects the in-game editor, not this settings window.");

        Toggle(tileReadoutSec.Body, def.ShowFloorBeats, conf.ShowFloorBeats,
            v => conf.ShowFloorBeats = v,
            "Show selected tiles' beats", "editor_show_floor_beats",
            "Shows the selected tiles' length in beats (total angle ÷ 180), on one of the tiles in the level editor. Affects the in-game editor, not this settings window.");

        Toggle(tileReadoutSec.Body, def.ShowFloorCount, conf.ShowFloorCount,
            v => conf.ShowFloorCount = v,
            "Show selected tiles' count", "editor_show_floor_count",
            "Shows how many tiles are selected, on one of the tiles in the level editor. Affects the in-game editor, not this settings window.");

        Toggle(tileReadoutSec.Body, def.ShowFloorDuration, conf.ShowFloorDuration,
            v => conf.ShowFloorDuration = v,
            "Show selected tiles' duration (in seconds)", "editor_show_floor_duration",
            "Shows how long the selected tiles take to play, in seconds, on one of the tiles in the level editor. Affects the in-game editor, not this settings window.");

        Toggle(tileReadoutSec.Body, def.UseTulttakModBehavior, conf.UseTulttakModBehavior,
            v => conf.UseTulttakModBehavior = v,
            "Prevent the last tile from being counted in timing calculation", "editor_use_tulttak_behavior",
            "Matches the Tulttak mod's behaviour by leaving the last selected tile out of the angle, beats, and duration totals.");
    }

    // === BGA Mod ===
    public static void BgaPage(RectTransform parent) {
        EditorFeature.EnsureConf();
        EditorSettings conf = EditorFeature.Conf;
        EditorSettings def = new();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);

        void Toggle(Transform body, bool dft, bool val, Action<bool> set, string label, string id, string desc) =>
            GenerateUI.ToggleTip(body, dft, val, v => { set(v); EditorFeature.Apply(); EditorFeature.Save(); }, label, id, desc);

        var bgaSec = GenerateUI.FlatSection(content.transform, "BGA Mod");

        Toggle(bgaSec.Body, def.BgaMod, conf.BgaMod,
            v => conf.BgaMod = v,
            "Hide tiles and planets", "editor_bga_mod",
            "Hides every tile and planet while the level is playing, so only the background shows — for recording a background animation (BGA) to composite gameplay over. The editor's edit view is unaffected; hiding only kicks in during play-test or actual gameplay.");

        Toggle(bgaSec.Body, def.BgaHideTileDeco, conf.BgaHideTileDeco,
            v => conf.BgaHideTileDeco = v,
            "Disable tile decorations", "editor_bga_hide_tile_deco",
            "Also hides decorations attached to tiles while BGA Mod is hiding the level. Background and camera-anchored decorations are left visible.");

        Toggle(bgaSec.Body, def.BgaHidePlanetDeco, conf.BgaHidePlanetDeco,
            v => conf.BgaHidePlanetDeco = v,
            "Disable planet decorations", "editor_bga_hide_planet_deco",
            "Also hides decorations attached to a planet while BGA Mod is hiding the level. Background and camera-anchored decorations are left visible.");
    }
}

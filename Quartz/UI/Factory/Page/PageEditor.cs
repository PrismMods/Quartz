using Quartz.Features.Editor;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static partial class PageEditor {
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
    public static void FlipRotatePage(RectTransform parent) {
        EditorFeature.EnsureConf();
        EditorSettings conf = EditorFeature.Conf;
        EditorSettings def = new();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        // Flip/rotate are per-call Harmony postfixes gated on the settings, so a
        // toggle only needs to persist — no reconcile/Apply pass like the tick
        // features on the other editor pages.
        void Toggle(Transform body, bool dft, bool val, Action<bool> set, string label, string id, string desc) =>
            GenerateUI.ToggleTip(body, dft, val, v => { set(v); EditorFeature.Save(); }, label, id, desc);
        var sec = GenerateUI.FlatSection(content.transform, "Flip & Rotate Tiles");
        Toggle(sec.Body, def.AdjustOnFlip, conf.AdjustOnFlip,
            v => conf.AdjustOnFlip = v,
            "Adjust Position Track when flipping", "editor_flip_adjust",
            "When you flip the selected tiles horizontally or vertically, also mirror the coordinates stored in each tile's Position Track events, so anything positioned by those events flips along with the tiles.");
        Toggle(sec.Body, def.AdjustOnRotate, conf.AdjustOnRotate,
            v => conf.AdjustOnRotate = v,
            "Adjust Position Track when rotating", "editor_rotate_adjust",
            "When you rotate the selected tiles, also rotate the coordinates stored in each tile's Position Track events by the same angle, so anything positioned by those events turns along with the tiles.");
        Toggle(sec.Body, def.CustomAngleRotation, conf.CustomAngleRotation,
            v => conf.CustomAngleRotation = v,
            "Use a custom rotation angle", "editor_custom_angle",
            "Rotate the selected tiles by the angle below instead of the default 90° when you use the rotate shortcut. The 180° rotate shortcut always stays 180°.");
        static float angleFilter(float v) => Mathf.Clamp(Mathf.Round(v), 1f, 359f);
        UISlider angle = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.CustomAngle,
            1f, 359f, conf.CustomAngle, angleFilter, null, null,
            "Rotation angle", "editor_custom_angle_value"
        );
        angle.Format = "0°";
        angle.OnChanged = v => conf.CustomAngle = v;
        angle.OnComplete = v => { conf.CustomAngle = v; EditorFeature.Save(); };
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_SHORTCUTS", "Shortcuts");
        void Help(string key, string text) =>
            GenerateUI.Localize(GenerateUI.AddMutedText(GenerateUI.Row(sec.Body)), key, text);
        Help("EDITOR_FLIPROTATE_SHORTCUT_FLIP_H", "Ctrl + L — Flip selection horizontally");
        Help("EDITOR_FLIPROTATE_SHORTCUT_FLIP_V", "Ctrl + Shift + L — Flip selection vertically");
        Help("EDITOR_FLIPROTATE_SHORTCUT_ROT_CCW", "Ctrl + , — Rotate selection counterclockwise");
        Help("EDITOR_FLIPROTATE_SHORTCUT_ROT_CW", "Ctrl + . — Rotate selection clockwise");
        Help("EDITOR_FLIPROTATE_SHORTCUT_ROT_180", "Ctrl + / — Rotate selection 180°");
    }
}

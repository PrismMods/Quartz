#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Features.Editor;
using Quartz.Features.MainMenu;
using Quartz.Features.Optimizer;
using Quartz.Features.SpecialDays;
using Quartz.Features.Tweaks;
using UnityEngine;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The Tweaks and Editor pages in the new menu, each against its classic page: rows, search hits, Korean text and a write
/// reaching its file, then the classic page seeing a switch, the hotkeys and the header switch's hotkey.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator TweaksEditor(Action<string, bool, string> check) {
        yield return TweaksGeneral(check);
        yield return TweaksSpecialDays(check);
        yield return TweaksOptimizer(check);
        yield return TweaksMainMenu(check);
        yield return EditorTileReadout(check);
        yield return EditorBga(check);
        yield return EditorFlipRotate(check);
        yield return EditorDecoPreview(check);
    }

    private static IEnumerator TweaksGeneral(Action<string, bool, string> check) {
        const string page = "tweaks.general";
        Tweaks.EnsureConf();
        TweaksSettings C() => Tweaks.Conf;
        string[] keys = ["TW_NOPAUSE", "TW_SCROLL"];
        yield return FeatureFormPage(check, "tweaks", page, Tweaks.ConfMgr, null, null, keys, keys, () => More());
        IEnumerator More() {
            bool was = C().DisableAutoPause;
            yield return Saved(check, page, "TW_NOPAUSE", () => ClickRow("TW_NOPAUSE"), () => C().DisableAutoPause != was, Tweaks.ConfMgr.Path);
            yield return Hotkey(check, page, "TW_NOPAUSE", "tw_nopause", "DESC_TW_NOPAUSE", Bind.To(() => C().DisableAutoPause, v => {
                C().DisableAutoPause = v;
                Tweaks.Save();
            }));
            yield return SearchKorean(check, "TW_NOPAUSE", "TWEAKS_GENERAL", "Korean initials find a Tweaks row");
        }
    }

    private static IEnumerator TweaksSpecialDays(Action<string, bool, string> check) {
        const string page = "tweaks.specialdays";
        SpecialDays.EnsureConf();
        string[] rows = ["SPECIALDAY_HALLOWEEN_LABEL", "SPECIALDAY_APRILFOOLS_LABEL"];
        yield return FeatureFormPage(check, "specialdays", page, SpecialDays.ConfMgr, null, null, ["SPECIALDAY_HINT", .. rows], rows, () => More());
        IEnumerator More() {
            SpecialDayMode want = SpecialDays.Conf.Halloween == SpecialDayMode.Off ? SpecialDayMode.On : SpecialDayMode.Off;
            string text = QuartzHost.Host.Translate(want == SpecialDayMode.On ? "SPECIALDAY_ON" : "SPECIALDAY_OFF");
            yield return Saved(check, page, "SPECIALDAY_HALLOWEEN_LABEL", () => PickItem("SPECIALDAY_HALLOWEEN_LABEL", text),
                () => SpecialDays.Conf.Halloween == want, SpecialDays.ConfMgr.Path);
        }
    }

    private static IEnumerator TweaksOptimizer(Action<string, bool, string> check) {
        const string page = "tweaks.optimizer";
        Optimizer.EnsureConf();
        OptimizerSettings C() => Optimizer.Conf;
        string[] keys = ["OPT_SMOOTHGC", "OPT_LEAKGUARD", "OPT_COLLECTONLOAD", "OPT_PRIORITY", "OPT_RUNINBG", "OPT_LOSSYTEXTURE", "OPT_FASTBLOOM",
            "OPT_SKIPNOOPFILTERS", "OPT_CACHESCREENSCALE", "OPT_SKIPIDLEPARTICLES", "OPT_PAUSEOFFSCREENPARTICLES", "OPT_RENDERHITSOUNDS"];
        yield return FeatureFormPage(check, "optimizer", page, Optimizer.ConfMgr, null, Optimizer.Apply, keys, keys, () => More());
        IEnumerator More() {
            bool was = C().FastBloom;
            yield return Saved(check, page, "OPT_FASTBLOOM", () => ClickRow("OPT_FASTBLOOM"), () => C().FastBloom != was, Optimizer.ConfMgr.Path);
            yield return Flips(check, page, "OPT_SKIPIDLEPARTICLES", Bind.To(() => C().SkipIdleParticles, v => {
                C().SkipIdleParticles = v;
                Optimizer.Apply();
                Optimizer.Save();
            }));
        }
    }

    private static IEnumerator TweaksMainMenu(Action<string, bool, string> check) {
        const string page = "tweaks.mainmenu";
        MenuTweaks.EnsureConf();
        string[] keys = ["TW_MENUMUSIC", "TW_MENUBPM", "TW_MENUSLOWBPM", "TW_MENUHIGHBPM"];
        yield return FeatureFormPage(check, "mainmenu", page, MenuTweaks.ConfMgr, null, null, keys, keys, () => More());
        IEnumerator More() {
            float want = MenuTweaks.Conf.MenuSlowBpm >= 300f ? 120f : 480f;
            yield return Saved(check, page, "TW_MENUSLOWBPM", () => SlideTo("TW_MENUSLOWBPM", 30f, 600f, want),
                () => Mathf.Abs(MenuTweaks.Conf.MenuSlowBpm - want) < 0.5f, MenuTweaks.ConfMgr.Path);
            yield return HeaderHotkeyCheck(check, page, "SECTION_MAIN_MENU", "mainmenu");
        }
    }

    private static IEnumerator EditorTileReadout(Action<string, bool, string> check) {
        string[] keys = ["EDITOR_SHOW_FLOOR_ANGLE", "EDITOR_SHOW_FLOOR_BEATS", "EDITOR_SHOW_FLOOR_COUNT", "EDITOR_SHOW_FLOOR_DURATION",
            "EDITOR_USE_TULTTAK_BEHAVIOR"];
        yield return EditorFormPage(check, "editor.tilereadout", keys, keys, EditorFeature.Apply, null);
    }

    private static IEnumerator EditorBga(Action<string, bool, string> check) {
        const string page = "editor.bga";
        string[] keys = ["EDITOR_BGA_MOD", "EDITOR_BGA_HIDE_TILE_DECO", "EDITOR_BGA_HIDE_PLANET_DECO"];
        yield return EditorFormPage(check, page, keys, keys, EditorFeature.Apply, () => More());
        IEnumerator More() {
            yield return Flips(check, page, "EDITOR_BGA_MOD", Bind.To(() => EditorFeature.Conf.BgaMod, v => {
                EditorFeature.Conf.BgaMod = v;
                EditorFeature.Apply();
                EditorFeature.Save();
            }));
            yield return HeaderHotkeyCheck(check, page, "SECTION_BGA_MOD", "editor");
            yield return SearchKorean(check, "EDITOR_BGA_MOD", "SECTION_BGA_MOD", "Korean initials find an Editor row");
        }
    }

    private static IEnumerator EditorFlipRotate(Action<string, bool, string> check) {
        const string page = "editor.fliprotate";
        string[] rows = ["EDITOR_FLIP_ADJUST", "EDITOR_ROTATE_ADJUST", "EDITOR_CUSTOM_ANGLE", "EDITOR_CUSTOM_ANGLE_VALUE"];
        string[] shortcuts = ["EDITOR_FLIPROTATE_SHORTCUT_FLIP_H", "EDITOR_FLIPROTATE_SHORTCUT_FLIP_V", "EDITOR_FLIPROTATE_SHORTCUT_ROT_CCW",
            "EDITOR_FLIPROTATE_SHORTCUT_ROT_CW", "EDITOR_FLIPROTATE_SHORTCUT_ROT_180"];
        EditorSettings C() => EditorFeature.Conf;
        yield return EditorFormPage(check, page, [.. rows, "HEADING_SHORTCUTS", .. shortcuts], rows, null, () => More());
        IEnumerator More() {
            bool was = C().AdjustOnFlip;
            yield return Saved(check, page, "EDITOR_FLIP_ADJUST", () => ClickRow("EDITOR_FLIP_ADJUST"), () => C().AdjustOnFlip != was,
                EditorFeature.ConfMgr.Path);
            // The header switch's check on editor.bga reloaded the feature, which drops its rows' hotkey flips; a bound key then
            // builds every page once before it fires. Built here first, so the timing below is the row's alone.
            FacetBridge.BuildAllPages();
            yield return Frames(1);
            yield return Hotkey(check, page, "EDITOR_FLIP_ADJUST", "editor_flip_adjust", "DESC_EDITOR_FLIP_ADJUST", Bind.To(() => C().AdjustOnFlip, v => {
                C().AdjustOnFlip = v;
                EditorFeature.Save();
            }));
        }
    }

    private static IEnumerator EditorDecoPreview(Action<string, bool, string> check) {
        yield return EditorFormPage(check, "editor.decopreview", ["EDITOR_DECO_PREVIEW", "EDITOR_DECO_PREVIEW_CREDIT"], ["EDITOR_DECO_PREVIEW"],
            EditorFeature.RefreshDecoPreview, null);
    }

    // The four Editor pages share the feature and its file.
    private static IEnumerator EditorFormPage(Action<string, bool, string> check, string page, string[] keys, string[] searched, Action apply,
        Func<IEnumerator> more) {
        EditorFeature.EnsureConf();
        yield return FeatureFormPage(check, "editor", page, EditorFeature.ConfMgr, null, apply, keys, searched, more);
    }
}
#endif

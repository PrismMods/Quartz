#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using Quartz.Features.HideJudgements;
using Quartz.Features.Interop;
using Quartz.Features.OttoIcon;
using Quartz.Features.PlanetColors;
using Quartz.Features.TileArc;
using Quartz.Features.UiHider;
using Quartz.Features.VisualTweaks;
using Quartz.IO;
using Quartz.IO.Interface;
using Quartz.UI.FacetUi.VisualsPages;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The Visuals pages in the new menu, each against its classic page: its rows, search hits and Korean text, a write
/// reaching its file, and each page's own rows (the judgement tiles, the hotkeys, the recording shortcut chord).
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator VisualsPages(Action<string, bool, string> check) {
        yield return VisualsHideJudgements(check);
        yield return VisualsTileArc(check);
        yield return VisualsTweaks(check);
        yield return VisualsPlanetColors(check);
        yield return VisualsOttoIcon(check);
        yield return VisualsUiHiding(check);
    }

    private static IEnumerator VisualsHideJudgements(Action<string, bool, string> check) {
        const string page = "visuals.hidejudgements";
        JudgementPopupHider.EnsureConf();
        bool x = XPerfectBridge.Installed;
        string[] tiles = [.. HideJudgementsPage.Rows(x).Select(r => r.Key)];
        int[] shown = [.. HideJudgementsPage.Rows(x).Select(r => r.Bit)];
        // A bit no tile shows: X Perfect's without XPerfect, Perfect's with it. All and None must keep it.
        int stray = x ? 3 : JudgementPopupHider.XPerfectPerfectBit;
        JudgementPopupHiderSettings C() => JudgementPopupHider.Conf;
        yield return FeatureFormPage(check, "hidejudgements", page, JudgementPopupHider.ConfMgr, () => C().HiddenMask |= 1 << stray, null,
            [.. tiles, "HIDEJUDGEMENTS_TILES"], tiles, () => More());
        IEnumerator More() {
            yield return MaskFlip(check, page, "JPOP_TOOEARLY", () => C().HiddenMask, 0, shown);
            yield return Hotkey(check, page, "JPOP_MISS", "jpop_miss", null, Bind.To(() => (C().HiddenMask >> 8 & 1) != 0, v => {
                C().HiddenMask = v ? C().HiddenMask | 1 << 8 : C().HiddenMask & ~(1 << 8);
                JudgementPopupHider.Save();
            }));
        }
    }

    private static IEnumerator VisualsTileArc(Action<string, bool, string> check) {
        const string page = "visuals.tilearc";
        TileArc.EnsureConf();
        yield return FeatureFormPage(check, "tilearc", page, TileArc.ConfMgr, null, TileArc.Refresh, ["TILEARC_INTENSITY"], ["TILEARC_INTENSITY"], () => More());
        IEnumerator More() {
            float want = Away(TileArc.Conf.Intensity * 100f);
            yield return Saved(check, page, "TILEARC_INTENSITY", () => SlideTo("TILEARC_INTENSITY", 0f, 100f, want),
                () => Mathf.Abs(TileArc.Conf.Intensity * 100f - want) < 0.5f, TileArc.ConfMgr.Path);
            yield return HeaderHotkeyCheck(check, page, "SECTION_TILE_ARC", "tilearc");
        }
    }

    private static IEnumerator VisualsTweaks(Action<string, bool, string> check) {
        const string page = "visuals.visualtweaks";
        VisualTweaks.EnsureConf();
        string[] keys = ["TW_CP", "TW_BCP", "TW_GLOW", "TW_PGLOW", "TW_SBPM", "TW_SBPM_SHOW_LABEL", "TW_SBPM_POS_LABEL"];
        yield return FeatureFormPage(check, "visualtweaks", page, VisualTweaks.ConfMgr, null, VisualTweaks.RefreshAll, keys, keys, () => More());
        IEnumerator More() {
            bool glow = VisualTweaks.Conf.DisableTileHitGlow;
            yield return Saved(check, page, "TW_GLOW", () => ClickRow("TW_GLOW"), () => VisualTweaks.Conf.DisableTileHitGlow != glow, VisualTweaks.ConfMgr.Path);
            yield return ClassicSees(check, page, "TW_GLOW", Bind.To(() => VisualTweaks.Conf.DisableTileHitGlow, v => {
                VisualTweaks.Conf.DisableTileHitGlow = v;
                VisualTweaks.RefreshTileHitGlowTweak();
                VisualTweaks.Save();
            }));
            yield return SearchKorean(check, "TW_GLOW", "SECTION_VISUAL_TWEAKS", "Korean initials find a Visuals row");
        }
    }

    private static IEnumerator VisualsPlanetColors(Action<string, bool, string> check) {
        const string page = "visuals.planetcolors";
        PlanetColors.EnsureConf();
        var rows = new List<string>();
        var headers = new List<string> { "HEADING_RING", "HEADING_PLANET_OVERLAY" };
        rows.AddRange(["PCOL_RECOLOR", "PCOL_SEP_TAIL"]);
        foreach(string n in new[] { "1", "2", "3" }) {
            headers.Add("HEADING_PLANET_" + n);
            rows.AddRange(["PCOL_BALL" + n, "PCOL_BALLOP" + n, "PCOL_TAIL" + n, "PCOL_TAILOP" + n]);
        }
        rows.AddRange(["PCOL_RINGON", "PCOL_SEP_RING"]);
        foreach(string n in new[] { "1", "2", "3" }) rows.AddRange(["PCOL_RINGCOL" + n, "PCOL_RINGOP" + n]);
        rows.Add("PCOL_OVERLAY_ON");
        foreach(string n in new[] { "1", "2", "3" }) {
            headers.Add("HEADING_PLANET_" + n + "#2");
            rows.AddRange(["PCOL_OVERLAY_PICK" + n, "PCOL_OVERLAY_CLEAR" + n, "PCOL_OVERLAY_SCALE" + n]);
        }
        PlanetColorsSettings C() => PlanetColors.Conf;
        yield return FeatureFormPage(check, "planetcolors", page, PlanetColors.ConfMgr, () => {
            C().SeparateTailColor = C().SeparateRingColor = C().EnableOverlay = true;
        }, () => {
            PlanetColors.ReloadOverlayImages();
            PlanetColors.Refresh();
        }, [.. rows, .. headers.Select(Unnumbered).Distinct()], [.. rows], () => More(), ["PCOL_RINGCOL", "PCOL_RINGOP"]);
        IEnumerator More() {
            // Parity reads the headers by their keys; the overlay block's second header of each planet by its own name.
            string[] seconds = ["HEADING_PLANET_1#2", "HEADING_PLANET_2#2", "HEADING_PLANET_3#2"];
            check($"{page}: each planet's overlay block has a header of its own", seconds.All(h => PageContent().Q(name: h) != null),
                $"missing={string.Join(",", seconds.Where(h => PageContent().Q(name: h) == null))}");
            float want = Away(C().BallOpacity[0] * 100f);
            yield return Saved(check, page, "PCOL_BALLOP1", () => SlideTo("PCOL_BALLOP1", 0f, 100f, want),
                () => Mathf.Abs(C().BallOpacity[0] * 100f - want) < 0.5f, PlanetColors.ConfMgr.Path);
        }
    }

    private static IEnumerator VisualsOttoIcon(Action<string, bool, string> check) {
        const string page = "visuals.ottoicon";
        OttoIcon.EnsureConf();
        string[] keys = ["OTTO_IMAGE", "OTTO_IMAGE_CLEAR", "OTTO_TINT_IMAGE", "OTTO_COLOR", "OTTO_HIGHBPM_ON", "OTTO_HIGHBPM_COLOR", "OTTO_OFFSET_X", "OTTO_OFFSET_Y"];
        yield return FeatureFormPage(check, "ottoicon", page, OttoIcon.ConfMgr, () => OttoIcon.Conf.UseHighBpmColor = true, OttoIcon.Refresh, keys, keys, () => More());
        IEnumerator More() {
            float want = Away(OttoIcon.Conf.OffsetX + 50f) - 50f;   // the -100 to 100 range, put on Away's 0 to 100
            yield return Saved(check, page, "OTTO_OFFSET_X", () => SlideTo("OTTO_OFFSET_X", -100f, 100f, want),
                () => Mathf.Abs(OttoIcon.Conf.OffsetX - want) < 0.5f, OttoIcon.ConfMgr.Path);
        }
    }

    private static IEnumerator VisualsUiHiding(Action<string, bool, string> check) {
        const string page = "visuals.uihiding";
        UiHider.EnsureConf();
        string[] flags = ["ALL", "JUDG", "MISS", "TITLE", "OTTO", "DIFF", "NOFAIL", "BETA", "RESULT", "METER", "FLASH", "SHORTCUT"];
        string[] rows = ["UIH_RECMODE", "UIH_USESHORTCUT", "UIH_SHORTCUT", .. flags.Select(f => "UIH_PLAY_" + f), .. flags.Select(f => "UIH_REC_" + f)];
        UiHiderSettings C() => UiHider.Conf;
        yield return FeatureFormPage(check, "uihider", page, UiHider.ConfMgr, null, UiHider.ApplyNow,
            [.. rows, "SECTION_PLAYING_PROFILE", "SECTION_RECORDING_PROFILE"], rows, () => More());
        IEnumerator More() {
            bool use = C().UseShortcut;
            yield return Saved(check, page, "UIH_USESHORTCUT", () => ClickRow("UIH_USESHORTCUT"), () => C().UseShortcut != use, UiHider.ConfMgr.Path);
            yield return Hotkey(check, page, "UIH_USESHORTCUT", "uih_useshortcut", null, Bind.To(() => C().UseShortcut, v => {
                C().UseShortcut = v;
                UiHider.Save();
            }));
            yield return ShortcutChord(check, page);
        }
    }

    // The recording shortcut: a chord written through the row lands in both fields and one undo puts both back; a capture
    // begun on the row ends in both fields. The key is one the player's shortcut does not hold, so each write is a change.
    // The page's restore puts the player's shortcut back.
    private static IEnumerator ShortcutChord(Action<string, bool, string> check, string page) {
        UiHiderSettings C() => UiHider.Conf;
        Control<string> row = KeyRow(PageContent().Q(name: "UIH_SHORTCUT-input"));
        (int mod, int key) was = (C().ShortcutModifier, C().ShortcutKey);
        KeyCode k = was.key == (int)KeyCode.F9 ? KeyCode.F10 : KeyCode.F9;
        string chord = Keybind.Format(Keybind.KeyModifier.Shift, k);
        row?.Set(chord);
        yield return Frames(2);
        bool landed = C().ShortcutModifier == (int)Keybind.KeyModifier.Shift && C().ShortcutKey == (int)k;
        bool undid = global::Facet.History.Undo();
        yield return Frames(2);
        check($"{page} UIH_SHORTCUT: a chord set here lands in both fields, and undo puts both back",
            row != null && landed && undid && (C().ShortcutModifier, C().ShortcutKey) == was,
            $"row={row != null} wrote='{chord}' landed={landed} undo={undid} now={C().ShortcutModifier}/{C().ShortcutKey} was={was.mod}/{was.key}");
        bool began = row != null && row.BeginCapture();
        yield return Frames(1);
        bool capturing = KeyCaptureService.IsCapturing;
        typeof(KeyCaptureService).GetMethod("End", Static).Invoke(null, [k]);   // as the capture runner ends it
        yield return Frames(2);
        check($"{page} UIH_SHORTCUT: a capture ends in both fields",
            began && capturing && C().ShortcutKey == (int)k && C().ShortcutModifier == (int)Keybind.KeyModifier.None,
            $"began={began} capturing={capturing} now={C().ShortcutModifier}/{C().ShortcutKey}");
        KeyCaptureService.Cancel();
        Popup.CloseAll();
    }
}
#endif

using Quartz.Core;
using Quartz.Features.UiHider;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PageUiHider {
    public static void Create(RectTransform parent) =>
        CreateUiHiding(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    private static void CreateUiHiding(Transform content) {
        UiHider.EnsureConf();
        UiHiderSettings conf = UiHider.Conf;
        UiHiderSettings def = new();
        var sec = GenerateUI.FlatSection(
            content, "UI Hiding",
            v => {
                conf.Enabled = v;
                if(v) UiHider.ApplyNow();
                else UiHider.Restore();
                UiHider.Save();
            },
            conf.Enabled,
            "Enable UI Hiding", "uihiding_enable", def.Enabled
        );
        GenerateUI.ToggleTip(
            sec.Body,
            def.RecordingMode,
            conf.RecordingMode,
            v => {
                conf.RecordingMode = v;
                UiHider.ApplyNow();
                UiHider.Save();
            },
            "Recording Mode",
            "uih_recmode",
            "Which profile is live right now: off = Playing, on = Recording."
        );
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.UseShortcut,
            conf.UseShortcut,
            v => {
                conf.UseShortcut = v;
                UiHider.Save();
            },
            "Use Recording Mode Shortcut",
            "uih_useshortcut"
        );
        GenerateUI.KeyBind(
            GenerateUI.Row(sec.Body),
            (Keybind.KeyModifier)conf.ShortcutModifier,
            (KeyCode)conf.ShortcutKey,
            (mod, key) => {
                conf.ShortcutModifier = (int)mod;
                conf.ShortcutKey = (int)key;
                UiHider.Save();
            },
            "Recording Mode Shortcut",
            "uih_shortcut"
        );
        void ProfileSection(string title, UiHiderProfile profile, UiHiderProfile defProfile, string idPrefix) {
            var prof = GenerateUI.Collapsible(sec.Body, title, startExpanded: false);
            void Flag(string label, string id, bool defVal, bool val, Action<bool> set) {
                GenerateUI.Toggle(
                    GenerateUI.Row(prof.Body),
                    defVal,
                    val,
                    v => {
                        set(v);
                        UiHider.ApplyNow();
                        UiHider.Save();
                    },
                    label,
                    idPrefix + id
                );
            }
            Flag("Hide Everything (No HUD)", "_all", defProfile.HideEverything, profile.HideEverything, v => profile.HideEverything = v);
            Flag("Hide Judgement Text", "_judg", defProfile.HideJudgment, profile.HideJudgment, v => profile.HideJudgment = v);
            Flag("Hide Miss Indicators", "_miss", defProfile.HideMissIndicators, profile.HideMissIndicators, v => profile.HideMissIndicators = v);
            Flag("Hide Level Title", "_title", defProfile.HideTitle, profile.HideTitle, v => profile.HideTitle = v);
            Flag("Hide Otto / Autoplay Text", "_otto", defProfile.HideOtto, profile.HideOtto, v => profile.HideOtto = v);
            Flag("Hide Difficulty Icon", "_diff", defProfile.HideTimingTarget, profile.HideTimingTarget, v => profile.HideTimingTarget = v);
            Flag("Hide No Fail Icon", "_nofail", defProfile.HideNoFailIcon, profile.HideNoFailIcon, v => profile.HideNoFailIcon = v);
            Flag("Hide Beta Build Text", "_beta", defProfile.HideBeta, profile.HideBeta, v => profile.HideBeta = v);
            Flag("Hide Result Text", "_result", defProfile.HideResult, profile.HideResult, v => profile.HideResult = v);
            Flag("Hide Hit Error Meter", "_meter", defProfile.HideHitErrorMeter, profile.HideHitErrorMeter, v => profile.HideHitErrorMeter = v);
            Flag("Hide Last Floor Flash", "_flash", defProfile.HideLastFloorFlash, profile.HideLastFloorFlash, v => profile.HideLastFloorFlash = v);
            Flag("Hide Shortcut Hints", "_shortcut", defProfile.HideShortcutHints, profile.HideShortcutHints, v => {
                profile.HideShortcutHints = v;
                UiHider.RefreshShortcutHints();
            });
        }
        ProfileSection("Playing Profile", conf.Playing, def.Playing, "uih_play");
        ProfileSection("Recording Profile", conf.Recording, def.Recording, "uih_rec");
    }
}

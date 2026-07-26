using Quartz.Core;
using Quartz.Features.Interop;
using Quartz.Features.Restriction;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PageRestriction {
    public static void JudgementPage(RectTransform parent) =>
        CreateJudgementRestriction(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    public static void DeathLimitPage(RectTransform parent) =>
        CreateDeathLimit(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    private static void CreateJudgementRestriction(Transform content) {
        Restriction.EnsureConf();
        RestrictionSettings conf = Restriction.Conf;
        RestrictionSettings def = new();
        if(conf.JRestrictMode == 2 && !XPerfectBridge.Installed) conf.JRestrictMode = 1;
        var sec = GenerateUI.FlatSection(
            content, "Judgement Restriction",
            v => {
                conf.JRestrictEnabled = v;
                Restriction.Save();
            },
            conf.JRestrictEnabled,
            "Enable Judgement Restriction", "judgementrestriction_enable", def.JRestrictEnabled
        );
        RectTransform accuracyRow = null;
        RectTransform[] maskRows = null;
        void RefreshConditionalRows() {
            accuracyRow?.gameObject.SetActive(conf.JRestrictMode == 0);
            if(maskRows == null) return;
            foreach(RectTransform row in maskRows) row?.gameObject.SetActive(conf.JRestrictMode == 3);
        }
        int[] modes = XPerfectBridge.Installed ? [0, 1, 2, 3, 4] : [0, 1, 3, 4];
        GenerateUI.DropDown(
            GenerateUI.Row(sec.Body),
            def.JRestrictMode,
            conf.JRestrictMode,
            modes,
            ModeName,
            v => {
                conf.JRestrictMode = v;
                RefreshConditionalRows();
                Restriction.Save();
            },
            "jr_mode"
        );
        accuracyRow = GenerateUI.Row(sec.Body);
        UISlider accuracy = GenerateUI.Slider(
            accuracyRow,
            def.JRestrictAccuracy, 0f, 100f, conf.JRestrictAccuracy,
            null, null, null,
            "Min Accuracy (%)",
            "jr_acc"
        );
        accuracy.Format = "0.0";
        accuracy.OnChanged = v => conf.JRestrictAccuracy = v;
        accuracy.OnComplete = v => {
            conf.JRestrictAccuracy = v;
            Restriction.Save();
        };
        (HitMargin Margin, string Label, string Id)[] entries = [
            (HitMargin.TooEarly, "Too Early", "jr_allow_tooearly"),
            (HitMargin.VeryEarly, "Very Early", "jr_allow_veryearly"),
            (HitMargin.EarlyPerfect, "Early Perfect", "jr_allow_earlyperfect"),
            (HitMargin.Perfect, "Perfect", "jr_allow_perfect"),
            (HitMargin.LatePerfect, "Late Perfect", "jr_allow_lateperfect"),
            (HitMargin.VeryLate, "Very Late", "jr_allow_verylate"),
            (HitMargin.TooLate, "Too Late", "jr_allow_toolate"),
            (HitMargin.Multipress, "Multipress", "jr_allow_multipress"),
            (HitMargin.FailMiss, "Miss", "jr_allow_miss"),
            (HitMargin.FailOverload, "Overload (No Fail)", "jr_allow_overload_nofail"),
            (HitMargin.OverPress, "Overload (Fail)", "jr_allow_overload_fail"),
        ];
        maskRows = new RectTransform[entries.Length];
        for(int i = 0; i < entries.Length; i++) {
            int bit = 1 << (int)entries[i].Margin;
            maskRows[i] = GenerateUI.Row(sec.Body);
            GenerateUI.Toggle(
                maskRows[i],
                (def.JRestrictAllowedMask & bit) != 0,
                (conf.JRestrictAllowedMask & bit) != 0,
                v => {
                    if(v) conf.JRestrictAllowedMask |= bit;
                    else conf.JRestrictAllowedMask &= ~bit;
                    Restriction.Save();
                },
                entries[i].Label,
                entries[i].Id
            );
        }
        var message = GenerateUI.Input(
            GenerateUI.Row(sec.Body),
            def.JRestrictMessage,
            conf.JRestrictMessage,
            v => {
                conf.JRestrictMessage = v;
                Restriction.Save();
            },
            "Restriction broken message",
            MainCore.Spr.Get(UISprite.Text128),
            "jr_message"
        );
        message.Rect.AddToolTip(
            "DESC_JR_MESSAGE",
            "Shown on the fail screen when the restriction kills the run."
        );
        var hintRow = GenerateUI.Row(sec.Body, 30f);
        var hint = GenerateUI.AddMutedText(hintRow, 16f, 0.45f);
        GenerateUI.Localize(hint, "JR_MESSAGE_HINT", "Use {judgement} for the judgement you broke.");
        RefreshConditionalRows();
    }
    private static string ModeName(int mode) => mode switch {
        0 => MainCore.Tr.Get("JR_MODE_MIN_ACCURACY", "Minimum Accuracy"),
        1 => MainCore.Tr.Get("JR_MODE_PURE_PERFECT", "Pure Perfect Only"),
        2 => MainCore.Tr.Get("JR_MODE_XPURE_PERFECT", "X-Perfect Only"),
        3 => MainCore.Tr.Get("JR_MODE_CUSTOM", "Custom Judgements"),
        4 => MainCore.Tr.Get("JR_MODE_NO_TOO_EARLY", "No Too Early"),
        _ => mode.ToString(),
    };
    private static void CreateDeathLimit(Transform content) {
        Restriction.EnsureConf();
        RestrictionSettings conf = Restriction.Conf;
        RestrictionSettings def = new();
        var sec = GenerateUI.FlatSection(
            content, "Death Limit",
            v => {
                conf.DeathLimitEnabled = v;
                Restriction.Save();
            },
            conf.DeathLimitEnabled,
            "Enable Death Limit", "deathlimit_enable", def.DeathLimitEnabled
        );
        void LimitPair(string toggleLabel, string sliderLabel, string id,
            bool defOn, bool on, Action<bool> setOn,
            int defMax, int max, Action<int> setMax, float sliderMax) {
            RectTransform sliderRow = null;
            GenerateUI.Toggle(
                GenerateUI.Row(sec.Body),
                defOn,
                on,
                v => {
                    setOn(v);
                    sliderRow?.gameObject.SetActive(v);
                    Restriction.Save();
                },
                toggleLabel,
                id + "_on"
            );
            sliderRow = GenerateUI.Row(sec.Body);
            UISlider slider = GenerateUI.Slider(
                sliderRow,
                defMax, 0f, sliderMax, max,
                v => Mathf.Round(v), null, null,
                sliderLabel,
                id + "_max"
            );
            slider.Format = "0";
            slider.OnChanged = v => setMax((int)v);
            slider.OnComplete = v => {
                setMax((int)v);
                Restriction.Save();
            };
            sliderRow.gameObject.SetActive(on);
        }
        LimitPair("Limit Deaths (Miss + Overload)", "Max Deaths", "dl_deaths",
            def.MaxDeathsOn, conf.MaxDeathsOn, v => conf.MaxDeathsOn = v,
            def.MaxDeaths, conf.MaxDeaths, v => conf.MaxDeaths = v, 100f);
        LimitPair("Limit Misses", "Max Misses", "dl_misses",
            def.MaxMissesOn, conf.MaxMissesOn, v => conf.MaxMissesOn = v,
            def.MaxMisses, conf.MaxMisses, v => conf.MaxMisses = v, 50f);
        LimitPair("Limit Overloads", "Max Overloads", "dl_overloads",
            def.MaxOverloadsOn, conf.MaxOverloadsOn, v => conf.MaxOverloadsOn = v,
            def.MaxOverloads, conf.MaxOverloads, v => conf.MaxOverloads = v, 50f);
        var message = GenerateUI.Input(
            GenerateUI.Row(sec.Body),
            def.DeathLimitMessage,
            conf.DeathLimitMessage,
            v => {
                conf.DeathLimitMessage = v;
                Restriction.Save();
            },
            "Limit reached message",
            MainCore.Spr.Get(UISprite.Text128),
            "dl_message"
        );
        message.Rect.AddToolTip(
            "DESC_DL_MESSAGE",
            "Shown on the fail screen when a limit kills the run."
        );
    }
}

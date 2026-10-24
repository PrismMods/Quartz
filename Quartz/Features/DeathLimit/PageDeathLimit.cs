using Quartz.Core;
using Quartz.Features.DeathLimit;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PageDeathLimit {
    public static void Create(RectTransform parent) =>
        CreateDeathLimit(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    private static void CreateDeathLimit(Transform content) {
        DeathLimit.EnsureConf();
        DeathLimitSettings conf = DeathLimit.Conf;
        DeathLimitSettings def = new();
        var sec = GenerateUI.FlatSection(
            content, "Death Limit",
            v => {
                conf.DeathLimitEnabled = v;
                DeathLimit.Save();
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
                    DeathLimit.Save();
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
                DeathLimit.Save();
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
                DeathLimit.Save();
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

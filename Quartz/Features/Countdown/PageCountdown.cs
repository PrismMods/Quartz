using Quartz.Features.Countdown;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static class PageCountdown {
    public static void Create(RectTransform parent) {
        CountdownFeature.EnsureConf();
        CountdownSettings conf = CountdownFeature.Conf;
        CountdownSettings def = new();
        RectTransform content = PageFactory.CreateScrollablePage(parent);
        void Save() => CountdownFeature.Save();
        GenerateUI.Localize(
            GenerateUI.AddTextH1(GenerateUI.Row(content.transform)),
            "SECTION_COUNTDOWN",
            "Countdown"
        );
        GenerateUI.ToggleTip(
            content.transform, def.Enabled, conf.Enabled,
            v => { conf.Enabled = v; CountdownFeature.EnabledChanged(); Save(); },
            "Enable Countdown", "countdown_enable",
            "Replaces the countdown you get when a run starts from a checkpoint or from a middle tile in the "
                + "level editor. Off, the game's own countdown is used."
        );
        UISlider min = GenerateUI.Slider(
            GenerateUI.Row(content.transform),
            def.MinBpm,
            100f, 1000f, conf.MinBpm, Mathf.Round, null, null,
            "Minimum Countdown Tempo", "countdown_minbpm"
        );
        UISlider max = GenerateUI.Slider(
            GenerateUI.Row(content.transform),
            def.MaxBpm,
            100f, 1000f, conf.MaxBpm, Mathf.Round, null, null,
            "Maximum Countdown Tempo", "countdown_maxbpm"
        );
        min.Format = "0 BPM";
        max.Format = "0 BPM";
        min.OnChanged = v => conf.MinBpm = v;
        min.OnComplete = v => {
            conf.MinBpm = v;
            if(conf.MaxBpm < v) {
                conf.MaxBpm = v;
                max.Set(v, invoke: false);
            }
            Save();
        };
        max.OnChanged = v => conf.MaxBpm = v;
        max.OnComplete = v => {
            conf.MaxBpm = v;
            if(conf.MinBpm > v) {
                conf.MinBpm = v;
                min.Set(v, invoke: false);
            }
            Save();
        };
    }
}

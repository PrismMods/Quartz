using Quartz.Core;
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
            v => { conf.Enabled = v; CountdownFeature.ModeChanged(); Save(); },
            "Enable Countdown", "countdown_enable",
            "Replaces the countdown you get when a run starts from a checkpoint or from a middle tile in the "
                + "level editor. Off, the game's own countdown is used."
        );
        GenerateUI.DropDown(
            GenerateUI.Row(content.transform),
            def.Mode,
            conf.Mode,
            new[] { CountdownMode.Haywire, CountdownMode.Metronome },
            m => m switch {
                CountdownMode.Metronome => MainCore.Tr.Get("COUNTDOWN_MODE_METRONOME", "Metronome"),
                _ => MainCore.Tr.Get("COUNTDOWN_MODE_HAYWIRE", "Haywire"),
            },
            v => { conf.Mode = v; CountdownFeature.ModeChanged(); Save(); },
            "countdown_mode",
            260f,
            "Countdown Mode"
        );
        BuildHaywire(content, conf, def, Save);
        BuildMetronome(content, conf, def, Save);
    }
    private static void BuildHaywire(
        RectTransform content,
        CountdownSettings conf,
        CountdownSettings def,
        Action save
    ) {
        GenerateUI.CollapsibleSection sec = GenerateUI.Collapsible(content.transform, "Haywire", true);
        GenerateUI.Localize(
            GenerateUI.AddMutedText(GenerateUI.Row(sec.Body)),
            "COUNTDOWN_HAYWIRE_ABOUT",
            "Keeps the game's own countdown but slows it down, by multiplying the lead-in tiles' speed by a power "
                + "of two until the countdown ticks land in the range below. The music is never re-pitched — the "
                + "audio seek is pulled back so the first hit still falls on its real beat."
        );
        UISlider min = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.MinBpm,
            100f, 1000f, conf.MinBpm, Mathf.Round, null, null,
            "Minimum Countdown Tempo", "countdown_minbpm"
        );
        UISlider max = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
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
            save();
        };
        max.OnChanged = v => conf.MaxBpm = v;
        max.OnComplete = v => {
            conf.MaxBpm = v;
            if(conf.MinBpm > v) {
                conf.MinBpm = v;
                min.Set(v, invoke: false);
            }
            save();
        };
    }
    private static void BuildMetronome(
        RectTransform content,
        CountdownSettings conf,
        CountdownSettings def,
        Action save
    ) {
        GenerateUI.CollapsibleSection sec = GenerateUI.Collapsible(content.transform, "Metronome", false);
        GenerateUI.Localize(
            GenerateUI.AddMutedText(GenerateUI.Row(sec.Body)),
            "COUNTDOWN_ABOUT",
            "Freezes the planets on the next manual tile's Pure Perfect timing while a metronome loops, and resumes "
                + "the run from that exact timestamp on your first input. Level-editor play-tests only."
        );
        GenerateUI.ToggleTip(
            sec.Body, def.ShowControlPanel, conf.ShowControlPanel,
            v => { conf.ShowControlPanel = v; save(); },
            "Show the metronome panel", "countdown_show_panel",
            "Shows a small panel at the bottom of the screen while the start is frozen, for changing the metronome "
                + "tempo and meter without leaving the play-test. Changes there are saved back to these settings."
        );
        GenerateUI.ToggleTip(
            sec.Body, def.ShowMetronomeIcon, conf.ShowMetronomeIcon,
            v => { conf.ShowMetronomeIcon = v; save(); },
            "Show the metronome icon", "countdown_show_icon",
            "Shows a metronome icon in the middle of the screen that swings with every click."
        );
        GenerateUI.ToggleTip(
            sec.Body, def.AnimatePlanets, conf.AnimatePlanets,
            v => { conf.AnimatePlanets = v; save(); },
            "Animate the planets while frozen", "countdown_animate_planets",
            "Loops the planet's approach to the next tile in time with the countdown, so the landing point stays "
                + "readable while frozen. Turn it off to leave the planets perfectly still."
        );
        GenerateUI.ToggleTip(
            sec.Body, def.UseCustomBpm, conf.UseCustomBpm,
            v => { conf.UseCustomBpm = v; save(); },
            "Use a fixed metronome tempo", "countdown_custom_bpm",
            "Off, the metronome follows the level's own countdown tempo, doubled or halved until it lands between "
                + "200 and 500 BPM. On, it always clicks at the tempo below."
        );
        UISlider bpm = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.ClickBpm,
            20f, 999f, conf.ClickBpm, v => Mathf.Round(v * 10f) / 10f, null, null,
            "Metronome Tempo", "countdown_bpm"
        );
        bpm.Format = "0.0 BPM";
        bpm.OnChanged = v => conf.ClickBpm = v;
        bpm.OnComplete = v => { conf.ClickBpm = v; conf.UseCustomBpm = true; save(); };
        UISlider numerator = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.Numerator,
            1f, 16f, conf.Numerator, Mathf.Round, null, null,
            "Beats per Bar", "countdown_numerator"
        );
        numerator.OnChanged = v => conf.Numerator = (int)v;
        numerator.OnComplete = v => { conf.Numerator = (int)v; save(); };
        UISlider denominator = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.Denominator,
            1f, 16f, conf.Denominator, Mathf.Round, null, null,
            "Note Value", "countdown_denominator"
        );
        denominator.OnChanged = v => conf.Denominator = (int)v;
        denominator.OnComplete = v => { conf.Denominator = (int)v; save(); };
        UISlider volume = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.Volume,
            0f, 1f, conf.Volume, v => Mathf.Round(v * 100f) / 100f, null, null,
            "Metronome Volume", "countdown_volume"
        );
        volume.Format = "0%";
        volume.OnChanged = v => conf.Volume = v;
        volume.OnComplete = v => { conf.Volume = v; save(); };
        GenerateUI.Localize(
            GenerateUI.AddMutedText(GenerateUI.Row(sec.Body)),
            "COUNTDOWN_CREDIT",
            "Enhanced Countdown by IMPL (KGH1113), ported with their permission."
        );
    }
}

using Quartz.Core;
using Quartz.Features.Practice;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static partial class PageGameplay {
    private static void CreatePractice(Transform content) {
        PracticeDifficulty.EnsureConf();
        PracticeSettings conf = PracticeDifficulty.Conf;
        PracticeSettings def = new();
        void Save() => PracticeDifficulty.Save();
        var sec = GenerateUI.FlatSection(
            content, "Practice Difficulty",
            v => {
                conf.Enabled = v;
                Save();
            },
            conf.Enabled,
            "Enable Practice Difficulty", "practice_enable"
        );
        GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(sec.Body, 46f),
            "PRACTICE_HINT",
            "Bind a key to a difficulty and a pitch. Both land between attempts, never mid-run: difficulty is held until the current attempt ends, and pitch is written into the level's own Pitch setting once, exactly as if you typed it in. The readout shows anything still queued with an arrow.",
            18f
        );
        GameObject list = new("PracticeBindings");
        list.transform.SetParent(sec.Body, false);
        list.AddComponent<RectTransform>();
        GenerateUI.FitVertical(list, 6f);
        Action rebuild = null;
        rebuild = () => {
            if(list == null) return;
            GenerateUI.ClearChildren(list.transform);
            GenerateUI.PruneSections();
            if(conf.Bindings.Count == 0) {
                GenerateUI.AddLocalizedMutedText(
                    GenerateUI.Row(list.transform), "PRACTICE_NO_BINDINGS", "No keys bound yet.", 19f);
                return;
            }
            for(int i = 0; i < conf.Bindings.Count; i++) BuildBindingRows(list.transform, conf, i, Save, rebuild);
        };
        GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            () => {
                if(conf.Bindings.Count >= 16) return;
                conf.Bindings.Add(new PracticeBinding());
                Save();
                rebuild();
            },
            "Add Key",
            "practice_add"
        ).Rect.AddToolTip(
            "DESC_PRACTICE_ADD",
            "Adds another key binding. Up to 16."
        );
        rebuild();
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "PRACTICE_INDICATOR", "Indicator");
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.ShowIndicator,
            conf.ShowIndicator,
            v => {
                conf.ShowIndicator = v;
                Save();
            },
            "Show Difficulty Readout",
            "practice_showindicator"
        ).Rect.AddToolTip(
            "DESC_PRACTICE_SHOWINDICATOR",
            "Draws the difficulty the game is actually on, which the in-game selector does not always keep up to date."
        );
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.ShowSpeed,
            conf.ShowSpeed,
            v => {
                conf.ShowSpeed = v;
                Save();
            },
            "Show Pitch",
            "practice_showspeed"
        ).Rect.AddToolTip(
            "DESC_PRACTICE_SHOWSPEED",
            "Shows the level's current Pitch next to the difficulty."
        );
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.IndicatorOnlyInGame,
            conf.IndicatorOnlyInGame,
            v => {
                conf.IndicatorOnlyInGame = v;
                Save();
            },
            "Only While Playing",
            "practice_ingameonly"
        );
        static float sizeFilter(float v) => Mathf.Clamp(Mathf.Round(v), 8f, 120f);
        UISlider fontSize = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.FontSize,
            8f, 120f, conf.FontSize, sizeFilter, null, null,
            "Font Size", "practice_fontsize"
        );
        fontSize.Format = "0 px";
        fontSize.OnChanged = v => {
            conf.FontSize = v;
            PracticeOverlay.Apply();
        };
        fontSize.OnComplete = v => {
            conf.FontSize = v;
            PracticeOverlay.Apply();
            Save();
        };
        GenerateUI.ColorPicker(
            GenerateUI.Row(sec.Body),
            def.GetColor(),
            conf.GetColor(),
            c => {
                conf.SetColor(c);
                PracticeOverlay.Apply();
            },
            c => {
                conf.SetColor(c);
                PracticeOverlay.Apply();
                Save();
            },
            "Text Color",
            "practice_color"
        );
        GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            PracticeOverlay.ResetPosition,
            "Reset Position",
            "practice_resetpos"
        ).SetSecondary();
    }
    private static void BuildBindingRows(Transform parent, PracticeSettings conf, int index,
        Action save, Action rebuild) {
        PracticeBinding binding = conf.Bindings[index];
        string idp = "practice" + index;
        GenerateUI.CollapsibleSection section = GenerateUI.Collapsible(
            parent,
            string.Format(MainCore.Tr.Get("PRACTICE_BINDING", "Key {0}"), index + 1),
            false
        );
        parent = section.Body;
        UIButton captureBtn = null;
        captureBtn = GenerateUI.Button(
            GenerateUI.Row(parent),
            () => {
                if(Features.KeyLimiter.KeyLimiter.IsCapturing) {
                    Features.KeyLimiter.KeyLimiter.CancelCapture();
                    return;
                }
                if(captureBtn?.Label != null)
                    captureBtn.Label.text = MainCore.Tr.Get("PRESS_A_KEY", "Press a key...");
                Features.KeyLimiter.KeyLimiter.StartCapture(
                    key => {
                        if(key != KeyCode.None) binding.Key = key;
                        save();
                        rebuild();
                    },
                    () => {
                        if(captureBtn?.Label != null) captureBtn.Label.text = KeyLabel(binding);
                    }
                );
            },
            KeyLabel(binding),
            idp + "_key"
        );
        captureBtn.Rect.AddToolTip("DESC_PRACTICE_KEY", "Press any key to bind it. Escape cancels.");
        GenerateUI.DropDown(
            GenerateUI.Row(parent),
            2,
            binding.Difficulty,
            new[] { 0, 1, 2 },
            PracticeDifficulty.DifficultyName,
            v => {
                binding.Difficulty = v;
                save();
            },
            idp + "_difficulty",
            260f,
            "Difficulty"
        );
        static float pitchFilter(float v) => PracticeSettings.ClampPitch(Mathf.RoundToInt(v));
        UISlider pitch = GenerateUI.Slider(
            GenerateUI.Row(parent),
            100f,
            PracticeSettings.MinPitch, PracticeSettings.MaxPitch, binding.Pitch, pitchFilter, null, null,
            "Pitch", idp + "_speed"
        );
        pitch.Format = "0 %";
        pitch.OnChanged = v => binding.Pitch = PracticeSettings.ClampPitch(Mathf.RoundToInt(v));
        pitch.OnComplete = v => {
            binding.Pitch = PracticeSettings.ClampPitch(Mathf.RoundToInt(v));
            save();
        };
        GenerateUI.Toggle(
            GenerateUI.Row(parent),
            true,
            binding.SetsDifficulty,
            v => {
                binding.SetsDifficulty = v;
                save();
            },
            "Applies Difficulty",
            idp + "_setsdiff"
        );
        GenerateUI.Toggle(
            GenerateUI.Row(parent),
            true,
            binding.SetsSpeed,
            v => {
                binding.SetsSpeed = v;
                save();
            },
            "Applies Pitch",
            idp + "_setsspeed"
        );
        GenerateUI.Button(
            GenerateUI.Row(parent),
            () => {
                conf.Bindings.RemoveAt(index);
                save();
                rebuild();
            },
            "Remove",
            idp + "_remove"
        ).SetSecondary();
    }
    private static string KeyLabel(PracticeBinding binding) => binding.Key == KeyCode.None
        ? MainCore.Tr.Get("PRACTICE_UNBOUND", "Click to bind a key")
        : binding.Key.ToString();
}

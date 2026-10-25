using System.Globalization;
using Quartz.Core;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using TMPro;
using UnityEngine;
namespace Quartz.Features.AntiChatter;
public static class PageAntiChatter {
    public static void Create(RectTransform parent) {
        Transform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        AntiChatter.EnsureConf();
        AntiChatterSettings conf = AntiChatter.Conf;
        AntiChatterSettings def = new();
        var sec = GenerateUI.FlatSection(content, "AntiChatter");
        UISlider threshold = GenerateUI.Slider(
            GenerateUI.Row(sec.Body),
            def.ThresholdMs, 0f, 100f, conf.ThresholdMs,
            v => Mathf.Round(v), null, null,
            "Threshold (ms)",
            "antichatter_ms"
        );
        threshold.Format = "0 ms";
        threshold.OnChanged = v => conf.ThresholdMs = v;
        threshold.OnComplete = v => {
            conf.ThresholdMs = v;
            AntiChatter.Save();
        };
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.HoldBounceProtection,
            conf.HoldBounceProtection,
            v => {
                conf.HoldBounceProtection = v;
                AntiChatter.Save();
            },
            "Hold Bounce Protection",
            "antichatter_hold"
        );
        RectTransform statsRow = GenerateUI.Row(sec.Body);
        statsRow.gameObject.AddComponent<StatsLabel>().Label = GenerateUI.AddMutedText(statsRow, 19f, 0.6f);
        GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            AntiChatter.ResetStats,
            "Reset Stats",
            "antichatter_reset_stats"
        ).SetSecondary();
    }
    private sealed class StatsLabel : MonoBehaviour {
        public TMP_Text Label;
        private float nextPoll;
        private void Update() {
            if(Label == null || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.25f;
            string text = string.Format(CultureInfo.InvariantCulture,
                MainCore.Tr.Get("ANTICHATTER_STATS", "Blocked: {0} · Last: {1} ({2} ms)"),
                AntiChatter.BlockedCount, AntiChatter.LastBlockedKey, AntiChatter.LastBlockedDeltaMs.ToString("0.0", CultureInfo.InvariantCulture));
            if(Label.text != text) Label.text = text;
        }
    }
}

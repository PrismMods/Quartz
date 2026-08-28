using System.Globalization;
using Quartz.Core;
using Quartz.UI.Generator;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
internal static class PageAccuracy {
    private const int MaxRows = 300;
    public static void AppendTo(Transform content) {
        Quartz.Features.Accuracy.AccuracyOverlay.EnsureConf();
        Quartz.Features.Accuracy.AccuracySettings conf = Quartz.Features.Accuracy.AccuracyOverlay.Conf;
        Quartz.Features.Accuracy.AccuracySettings def = new();
        void Save() => Quartz.Features.Accuracy.AccuracyOverlay.Save();
        var sec = GenerateUI.FlatSection(
            content, "Accuracy",
            v => { conf.Enabled = v; Save(); },
            conf.Enabled,
            "Too Much Accuracy", "accuracy_enable", def.Enabled
        );
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_CURVE", "Scoring Curve");
        GenerateUI.SnapSlider(sec.Body, "Perfect Window", "accuracy_window",
            (float)def.WindowMs, 0f, 30f, (float)conf.WindowMs, "0.0 ms", 0.1f,
            v => conf.WindowMs = v, null, Save);
        GenerateUI.SnapSlider(sec.Body, "Max Deviation", "accuracy_maxdev",
            (float)def.MaxDeviationMs, 10f, 200f, (float)conf.MaxDeviationMs, "0 ms", 1f,
            v => conf.MaxDeviationMs = v, null, Save);
        GenerateUI.SnapSlider(sec.Body, "Curve Exponent", "accuracy_curve",
            (float)def.CurveExponent, 0.5f, 4f, (float)conf.CurveExponent, "0.00", 0.05f,
            v => conf.CurveExponent = v, null, Save);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_COMBO", "Combo");
        GenerateUI.SnapSlider(sec.Body, "Combo Threshold", "accuracy_combothreshold",
            def.ComboThreshold, 0f, 100f, conf.ComboThreshold, "0", 1f,
            v => conf.ComboThreshold = Mathf.RoundToInt(v), null, Save);
        GenerateUI.SnapSlider(sec.Body, "Empty Press Tolerance", "accuracy_emptytolerance",
            def.EmptyPressTolerance, 0f, 30f, conf.EmptyPressTolerance, "0", 1f,
            v => conf.EmptyPressTolerance = Mathf.RoundToInt(v), null, Save);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_PENALTIES", "Penalties");
        GenerateUI.SnapSlider(sec.Body, "Empty Press Penalty", "accuracy_emptypenalty",
            (float)def.EmptyPressPenalty, -100f, 0f, (float)conf.EmptyPressPenalty, "0", 1f,
            v => conf.EmptyPressPenalty = v, null, Save);
        GenerateUI.SnapSlider(sec.Body, "Miss Penalty", "accuracy_misspenalty",
            (float)def.MissPenalty, -200f, 0f, (float)conf.MissPenalty, "0", 1f,
            v => conf.MissPenalty = v, null, Save);
        GenerateUI.SnapSlider(sec.Body, "Overload Penalty", "accuracy_overloadpenalty",
            (float)def.OverloadPenalty, -200f, 0f, (float)conf.OverloadPenalty, "0", 1f,
            v => conf.OverloadPenalty = v, null, Save);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_DISPLAY", "Display");
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body), def.ShowHitText, conf.ShowHitText,
            v => { conf.ShowHitText = v; Save(); },
            "Show Score In Hit Text", "accuracy_hittext"
        );
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body), def.ShowDeathMarkers, conf.ShowDeathMarkers,
            v => { conf.ShowDeathMarkers = v; Save(); },
            "Show Death Markers", "accuracy_deathmarkers"
        );
        GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            Quartz.Features.Accuracy.DeathMarker.Clear,
            "Clear Death Markers",
            "accuracy_deathmarkers_clear"
        ).SetSecondary();
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_LAST_RUN", "Last Run");
        TextMeshProUGUI summary = GenerateUI.AddText(GenerateUI.Row(sec.Body, 60f), true);
        summary.text = string.Format(
            CultureInfo.InvariantCulture,
            "TMA {0:0.0000}%  ({1} tiles, max combo {2})",
            Quartz.Features.Accuracy.TmaScore.CachedAccuracy / 10000.0,
            Quartz.Features.Accuracy.TmaScore.Tiles,
            Quartz.Features.Accuracy.TmaScore.MaxCombo
        );
        TextMeshProUGUI exportStatus = GenerateUI.AddMutedText(GenerateUI.Row(sec.Body, 34f));
        GenerateUI.Button(
            GenerateUI.Row(sec.Body),
            () => exportStatus.text = "Exported to " + Quartz.Features.Accuracy.AccuracyExport.ExportLastRun(),
            "Export Last Run (JSON)",
            "accuracy_export"
        ).SetSecondary();
        var records = Quartz.Features.Accuracy.AccuracyRecorder.Records;
        int start = records.Count > MaxRows ? records.Count - MaxRows : 0;
        if(records.Count > MaxRows) {
            TextMeshProUGUI notice = GenerateUI.AddMutedText(GenerateUI.Row(sec.Body, 30f));
            notice.text = $"(showing last {MaxRows} of {records.Count} tiles)";
        }
        for(int i = start; i < records.Count; i++) {
            Quartz.Features.Accuracy.AccuracyRecord record = records[i];
            TextMeshProUGUI row = GenerateUI.AddText(GenerateUI.Row(sec.Body, 28f), true);
            row.fontSize = 17f;
            row.text = string.Format(
                CultureInfo.InvariantCulture,
                "#{0}  {1}  {2:+0.0;-0.0}ms  score {3:0}  combo {4}",
                record.Tile, record.Margin, record.DeviationMs, record.Score, record.Combo
            );
        }
    }
    public static void Create(RectTransform parent) =>
        AppendTo(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
}

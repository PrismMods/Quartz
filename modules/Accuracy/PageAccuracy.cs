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
        GenerateUI.ToggleTip(
            sec.Body, def.JeaEnabled, conf.JeaEnabled,
            v => { conf.JeaEnabled = v; Save(); },
            "JEA Accuracy", "accuracy_jea_enable",
            "Angular banded scoring (Just Enough Accuracy), normalized to a reference BPM."
        );
        GenerateUI.ToggleTip(
            sec.Body, def.NeaEnabled, conf.NeaEnabled,
            v => { conf.NeaEnabled = v; Save(); },
            "NEA Accuracy", "accuracy_nea_enable",
            "Millisecond-deviation scoring (Not Enough Accuracy)."
        );
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
            "JEA {0:0.0000}% ({1} tiles)  |  NEA {2:0.0000}% ({3} tiles)",
            Quartz.Features.Accuracy.JeaScore.CachedAccuracy / 10000.0,
            Quartz.Features.Accuracy.JeaScore.Tiles,
            Quartz.Features.Accuracy.NeaScore.CachedAccuracy / 10000.0,
            Quartz.Features.Accuracy.NeaScore.Tiles
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
                "#{0}  {1}  {2:+0.0;-0.0}ms  J{3:0}  N{4}",
                record.Tile, record.Margin, record.DeviationMs, record.JeaScore, record.NeaScore
            );
        }
    }
    public static void Create(RectTransform parent) =>
        AppendTo(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
}

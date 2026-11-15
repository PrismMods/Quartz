#if !IL2CPP
using Facet;
using Quartz.Features.Countdown;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.GameplayPages;
/// <summary>Gameplay ▸ Countdown in the new menu: both tempo limits on one range. An end moved past the other pushes it at
/// once (the classic page pushes it on release); the stored pair is the same, and one undo puts both ends back.</summary>
internal static class CountdownPage {
    private static CountdownSettings C => CountdownFeature.Conf;
    private static readonly CountdownSettings Def = new();

    internal static void Build(VisualElement p) {
        CountdownFeature.EnsureConf();
        ShellPage.Feature(p, "countdown", "SECTION_COUNTDOWN", "COUNTDOWN_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        VisualElement card = Controls.Card(p, null);
        Controls.Range(card, "COUNTDOWN_MINBPM", "COUNTDOWN_MAXBPM", QuartzShell.Writes(() => new Vector2(C.MinBpm, C.MaxBpm), v => {
            C.MinBpm = v.x;
            C.MaxBpm = v.y;
        }), o => {
            o.Min = 100f;
            o.Max = 1000f;
            o.Step = 1f;
            o.Format = "0 BPM";
            o.Default = new Vector2(Def.MinBpm, Def.MaxBpm);
        });
        Controls.AfterChange(card, CountdownFeature.Save);
    }
}
#endif

#if !IL2CPP
using Facet;
using Quartz.Features.HideJudgements;
using Quartz.Features.Interop;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.VisualsPages;
/// <summary>Visuals ▸ Hide Judgements in the new menu: the classic page's toggles as coloured tiles over the hidden mask.</summary>
internal static class HideJudgementsPage {
    private static readonly JudgementPopupHiderSettings Def = new();

    internal static void Build(VisualElement p) {
        JudgementPopupHider.EnsureConf();
        ShellPage.Feature(p, "hidejudgements", "SECTION_HIDE_JUDGEMENTS", "HIDEJUDGEMENTS_DESC", null, Cards);
    }

    /// <summary>The tiles in the classic page's order. With XPerfect, X, + and − Perfect stand in for Perfect, whose bit is
    /// then left as stored.</summary>
    internal static JudgementTiles.Row[] Rows(bool xperfect) => [
        new("JPOP_TOOEARLY", "jpop_tooearly", 0),
        new("JPOP_VERYEARLY", "jpop_veryearly", 1),
        new("JPOP_EARLYPERFECT", "jpop_earlyperfect", 2),
        .. xperfect
            ? new JudgementTiles.Row[] {
                new("JPOP_XPERFECT", "jpop_xperfect", JudgementPopupHider.XPerfectPerfectBit),
                new("JPOP_PLUSPERFECT", "jpop_plusperfect", JudgementPopupHider.PlusPerfectBit),
                new("JPOP_MINUSPERFECT", "jpop_minusperfect", JudgementPopupHider.MinusPerfectBit),
            }
            : [new("JPOP_PERFECT", "jpop_perfect", 3)],
        new("JPOP_LATEPERFECT", "jpop_lateperfect", 4),
        new("JPOP_VERYLATE", "jpop_verylate", 5),
        new("JPOP_TOOLATE", "jpop_toolate", 6),
        new("JPOP_MULTIPRESS", "jpop_multipress", 7),
        new("JPOP_MISS", "jpop_miss", 8),
        new("JPOP_OVERLOAD_NOFAIL", "jpop_overload_nofail", 9),
        new("JPOP_AUTO", "jpop_auto", 10),
        new("JPOP_OVERLOAD_FAIL", "jpop_overload_fail", 11),
    ];

    private static void Cards(VisualElement p) =>
        JudgementTiles.Grid(p, "HIDEJUDGEMENTS_TILES", Bind.To(() => JudgementPopupHider.Conf.HiddenMask, v => JudgementPopupHider.Conf.HiddenMask = v),
            Def.HiddenMask, Rows(XPerfectBridge.Installed), JudgementPopupHider.Save);
}
#endif

#if !IL2CPP
using Facet;
using Quartz.Features.HideJudgements;
using Quartz.Features.Interop;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.VisualsPages;
/// <summary>Visuals ▸ Hide Judgements in the new menu: the classic page's toggles as the judgement bar over the hidden mask.</summary>
internal static class HideJudgementsPage {
    private static readonly JudgementPopupHiderSettings Def = new();

    internal static void Build(VisualElement p) {
        JudgementPopupHider.EnsureConf();
        ShellPage.Feature(p, "hidejudgements", "SECTION_HIDE_JUDGEMENTS", "HIDEJUDGEMENTS_DESC", null, Cards);
    }

    private static readonly JudgementTiles.Row[] All = [
        new("JPOP_TOOEARLY", "jpop_tooearly", 0),
        new("JPOP_VERYEARLY", "jpop_veryearly", 1),
        new("JPOP_EARLYPERFECT", "jpop_earlyperfect", 2),
        new("JPOP_PERFECT", "jpop_perfect", 3),
        new("JPOP_XPERFECT", "jpop_xperfect", JudgementPopupHider.XPerfectPerfectBit),
        new("JPOP_PLUSPERFECT", "jpop_plusperfect", JudgementPopupHider.PlusPerfectBit),
        new("JPOP_MINUSPERFECT", "jpop_minusperfect", JudgementPopupHider.MinusPerfectBit),
        new("JPOP_LATEPERFECT", "jpop_lateperfect", 4),
        new("JPOP_VERYLATE", "jpop_verylate", 5),
        new("JPOP_TOOLATE", "jpop_toolate", 6),
    ];

    /// <summary>The bar's tiles, early to late (PageRules.BarBits). With X shown, X, + and − Perfect stand in for Perfect, whose bit
    /// is then left as stored.</summary>
    internal static JudgementTiles.Row[] Bar(bool x, bool minusEarly) => [.. PageRules.BarBits(x, minusEarly).Select(b => All.First(r => r.Bit == b))];

    /// <summary>The judgements that are not timings: a tile line under the bar.</summary>
    internal static readonly JudgementTiles.Row[] Extra = [
        new("JPOP_MULTIPRESS", "jpop_multipress", 7),
        new("JPOP_MISS", "jpop_miss", 8),
        new("JPOP_OVERLOAD_NOFAIL", "jpop_overload_nofail", 9),
        new("JPOP_AUTO", "jpop_auto", 10),
        new("JPOP_OVERLOAD_FAIL", "jpop_overload_fail", 11),
    ];

    /// <summary>Every tile the page shows: the bar, then the rest.</summary>
    internal static JudgementTiles.Row[] Rows(bool x, bool minusEarly) => [.. Bar(x, minusEarly), .. Extra];

    private static void Cards(VisualElement p) =>
        JudgementBar.Build(p, "HIDEJUDGEMENTS_TILES", Bind.To(() => JudgementPopupHider.Conf.HiddenMask, v => JudgementPopupHider.Conf.HiddenMask = v),
            Def.HiddenMask, Bar(XPerfectBridge.Shown, XPerfectBridge.Native), Extra, JudgementPopupHider.Save);
}
#endif

using Quartz.Features.KeyLimiter;
using Quartz.UI.Generator;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static partial class PageKeyLimiter {
    private static void CreateChartKeyLimiter(Transform body) {
        ChartKeyLimiter.EnsureConf();
        ChartKeyLimiterSettings conf = ChartKeyLimiter.Conf;
        if(conf == null) return;
        ChartKeyLimiterSettings def = new();
        GenerateUI.Localize(
            GenerateUI.AddTextH1(GenerateUI.Row(body)),
            "KL_CHART_SECTION",
            "Chart Key Limiter"
        );
        GenerateUI.Toggle(
            GenerateUI.Row(body),
            def.Enabled,
            conf.Enabled,
            v => {
                conf.Enabled = v;
                ChartKeyLimiter.Save();
                ChartKeyLimiter.Apply();
            },
            "Enable Chart Key Limiter",
            "chartkeylimiter_enable"
        );
        GenerateUI.AddLocalizedMutedText(
            GenerateUI.Row(body, 76f),
            "KL_CHART_NOTE",
            "Adds a Key Limiter event to the level editor. A chart can cap how many distinct keys "
                + "you may use from one tile onward and pick what an extra press does: ignore it, "
                + "kill the run, or fire tagged events. Reopen the level after changing this."
        );
    }
}

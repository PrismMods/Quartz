#if !IL2CPP
using Facet;
using Quartz.Features.DeathLimit;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.GameplayPages;
/// <summary>Gameplay ▸ Death Limit in the new menu, row for row as PageDeathLimit: each limit's slider shows while its switch
/// is on and saves on release.</summary>
internal static class DeathLimitPage {
    private static DeathLimitSettings C => DeathLimit.Conf;
    private static readonly DeathLimitSettings Def = new();

    internal static void Build(VisualElement p) {
        DeathLimit.EnsureConf();
        ShellPage.Feature(p, "deathlimit", "SECTION_DEATH_LIMIT", "DEATHLIMIT_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        VisualElement card = Controls.Card(p, null);
        Pair(card, "dl_deaths", Bind.To(() => C.MaxDeathsOn, v => C.MaxDeathsOn = v), Def.MaxDeathsOn,
            Bind.To(() => (float)C.MaxDeaths, v => C.MaxDeaths = Mathf.RoundToInt(v)), Def.MaxDeaths, 100f);
        Pair(card, "dl_misses", Bind.To(() => C.MaxMissesOn, v => C.MaxMissesOn = v), Def.MaxMissesOn,
            Bind.To(() => (float)C.MaxMisses, v => C.MaxMisses = Mathf.RoundToInt(v)), Def.MaxMisses, 50f);
        Pair(card, "dl_overloads", Bind.To(() => C.MaxOverloadsOn, v => C.MaxOverloadsOn = v), Def.MaxOverloadsOn,
            Bind.To(() => (float)C.MaxOverloads, v => C.MaxOverloads = Mathf.RoundToInt(v)), Def.MaxOverloads, 50f);
        ShellRows.Text(card, "DL_MESSAGE", Bind.To(() => C.DeathLimitMessage, v => C.DeathLimitMessage = v), 0, null, DeathLimit.Save, o => {
            o.Default = Def.DeathLimitMessage;
            o.Tooltip = "DESC_DL_MESSAGE";
        });
    }

    // id is the classic pair's id stem: the switch is id_on, the slider id_max.
    private static void Pair(VisualElement card, string id, Bind<bool> on, bool defOn, Bind<float> max, int defMax, float top) {
        string key = id.ToUpperInvariant();
        ShellRows.Toggle(card, key + "_ON", id + "_on", on, null, DeathLimit.Save, o => o.Default = defOn);
        ShellRows.ReleaseSlider(card, key + "_MAX", max, 0f, top, 1f, "0", null, DeathLimit.Save, o => {
            o.Default = defMax;
            o.VisibleWhen = on.Get;
        });
    }
}
#endif

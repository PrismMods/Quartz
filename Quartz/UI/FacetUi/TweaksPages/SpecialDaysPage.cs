#if !IL2CPP
using Facet;
using Quartz.Features.SpecialDays;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.TweaksPages;
/// <summary>Tweaks ▸ Special Days in the new menu: the hint and one mode per event, as PageSpecialDays.</summary>
internal static class SpecialDaysPage {
    private static SpecialDaysSettings C => SpecialDays.Conf;
    private static readonly SpecialDaysSettings Def = new();

    internal static void Build(VisualElement p) {
        SpecialDays.EnsureConf();
        ShellPage.Feature(p, "specialdays", "SECTION_SPECIAL_DAYS", "SPECIALDAY_HINT", null, Cards);
        ShellPage.Described(p, "SECTION_SPECIAL_DAYS", "SPECIALDAY_HINT");
    }

    private static void Cards(VisualElement p) {
        VisualElement card = Controls.Card(p, null);
        Mode(card, "SPECIALDAY_HALLOWEEN_LABEL", () => C.Halloween, v => C.Halloween = v, Def.Halloween);
        Mode(card, "SPECIALDAY_APRILFOOLS_LABEL", () => C.AprilFools, v => C.AprilFools = v, Def.AprilFools);
    }

    private static void Mode(VisualElement card, string key, Func<SpecialDayMode> get, Action<SpecialDayMode> set, SpecialDayMode def) =>
        Controls.Dropdown(card, key, QuartzShell.Writes(get, v => {
            set(v);
            SpecialDays.Save();
        }), [SpecialDayMode.Auto, SpecialDayMode.On, SpecialDayMode.Off], m => m switch {
            SpecialDayMode.On => "SPECIALDAY_ON",
            SpecialDayMode.Off => "SPECIALDAY_OFF",
            _ => "SPECIALDAY_AUTO",
        }, o => {
            o.Id = key;
            o.Default = def;
        });
}
#endif

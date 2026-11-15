#if !IL2CPP
using Facet;
using Quartz.Features.Tweaks;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.TweaksPages;
/// <summary>Tweaks ▸ General in the new menu, row for row as PageTweaks.</summary>
internal static class TweaksPage {
    private static TweaksSettings C => Tweaks.Conf;
    private static readonly TweaksSettings Def = new();

    internal static void Build(VisualElement p) {
        Tweaks.EnsureConf();
        ShellPage.Feature(p, "tweaks", "TWEAKS_GENERAL", "TWEAKS_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        VisualElement card = Controls.Card(p, null);
        ShellRows.Toggle(card, "TW_NOPAUSE", "tw_nopause", Bind.To(() => C.DisableAutoPause, v => C.DisableAutoPause = v), null, Tweaks.Save,
            o => { o.Default = Def.DisableAutoPause; o.Tooltip = "DESC_TW_NOPAUSE"; });
        ShellRows.Toggle(card, "TW_SCROLL", "tw_scroll", Bind.To(() => C.BlockMouseWheelScrollWhilePlaying, v => C.BlockMouseWheelScrollWhilePlaying = v),
            null, Tweaks.Save, o => { o.Default = Def.BlockMouseWheelScrollWhilePlaying; o.Tooltip = "DESC_TW_SCROLL"; });
    }
}
#endif

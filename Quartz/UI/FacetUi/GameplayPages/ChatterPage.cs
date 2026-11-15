#if !IL2CPP
using Facet;
using Quartz.Features.KeyLimiter;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.GameplayPages;
/// <summary>Gameplay ▸ Keyboard Chatter Blocker in the new menu. Key Limiter's module owns this page, so the header switch is
/// that feature's; the blocker's own switch is the first row.</summary>
internal static class ChatterPage {
    private static ChatterBlockerSettings C => ChatterBlocker.Conf;
    private static readonly ChatterBlockerSettings Def = new();

    internal static void Build(VisualElement p) {
        ChatterBlocker.EnsureConf();
        ShellPage.Feature(p, "keylimiter", "SECTION_KEYBOARD_CHATTER_BLOCKER", "CHATTER_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        VisualElement card = Controls.Card(p, null);
        ShellRows.Toggle(card, "CHATTERBLOCKER_ENABLE", "chatterblocker_enable", Bind.To(() => C.Enabled, v => C.Enabled = v), null,
            ChatterBlocker.Save, o => o.Default = Def.Enabled);
        ShellRows.ReleaseSlider(card, "KCB_MS", Bind.To(() => C.ThresholdMs, v => C.ThresholdMs = v), 0f, 100f, 1f, "0 ms", null,
            ChatterBlocker.Save, o => o.Default = Def.ThresholdMs);
    }
}
#endif

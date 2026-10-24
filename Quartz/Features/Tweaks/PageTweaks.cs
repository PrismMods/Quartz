using Quartz.Features.Tweaks;
using Quartz.UI.Generator;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PageTweaks {
    public static void GeneralPage(RectTransform parent) {
        Tweaks.EnsureConf();
        TweaksSettings conf = Tweaks.Conf;
        TweaksSettings def = new();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(content.transform)), "TWEAKS_GENERAL", "General");
        GenerateUI.ToggleTip(
            content.transform,
            def.DisableAutoPause,
            conf.DisableAutoPause,
            v => { conf.DisableAutoPause = v; Tweaks.Save(); },
            "Disable Auto Pause",
            "tw_nopause",
            "While auto-play is on, the game pauses itself (e.g. when the window loses focus). This blocks those automatic pauses \u2014 pausing manually still works."
        );
        GenerateUI.ToggleTip(
            content.transform,
            def.BlockMouseWheelScrollWhilePlaying,
            conf.BlockMouseWheelScrollWhilePlaying,
            v => { conf.BlockMouseWheelScrollWhilePlaying = v; Tweaks.Save(); },
            "Block Scroll While Playing",
            "tw_scroll",
            "Ignores mouse wheel input while a level is being played, so accidental scrolling can't affect the game mid-run."
        );
    }
}

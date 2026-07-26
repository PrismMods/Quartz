using Quartz.Features.Tweaks;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
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
            "While auto-play is on, the game pauses itself (e.g. when the window loses focus). This blocks those automatic pauses — pausing manually still works."
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
    public static void MainMenuPage(RectTransform parent) {
        Tweaks.EnsureConf();
        TweaksSettings conf = Tweaks.Conf;
        TweaksSettings def = new();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        var mainMenuSec = GenerateUI.FlatSection(content.transform, "Main Menu");
        GenerateUI.ToggleTip(
            mainMenuSec.Body,
            def.DisableMenuMusic,
            conf.DisableMenuMusic,
            v => { conf.DisableMenuMusic = v; Tweaks.Save(); },
            "Disable Menu Music",
            "tw_menumusic",
            "Mutes the theme song on the title and island-select screens. Takes effect immediately; gameplay music is untouched."
        );
        GenerateUI.ToggleTip(
            mainMenuSec.Body,
            def.MenuBpmEnabled,
            conf.MenuBpmEnabled,
            v => { conf.MenuBpmEnabled = v; Tweaks.Save(); },
            "Custom Menu BPM",
            "tw_menubpm",
            "Sets the menu rabbit's two speeds to the BPMs below instead of the default 1x / 2x. Re-open the menu to apply."
        );
        UISlider slowBpm = GenerateUI.Slider(
            GenerateUI.Row(mainMenuSec.Body),
            def.MenuSlowBpm, 30f, 600f, conf.MenuSlowBpm,
            Mathf.Round, v => conf.MenuSlowBpm = v,
            v => { conf.MenuSlowBpm = v; Tweaks.Save(); },
            "Slow BPM", "tw_menuslowbpm"
        );
        slowBpm.Format = "0";
        UISlider highBpm = GenerateUI.Slider(
            GenerateUI.Row(mainMenuSec.Body),
            def.MenuHighBpm, 30f, 600f, conf.MenuHighBpm,
            Mathf.Round, v => conf.MenuHighBpm = v,
            v => { conf.MenuHighBpm = v; Tweaks.Save(); },
            "High BPM", "tw_menuhighbpm"
        );
        highBpm.Format = "0";
    }
}

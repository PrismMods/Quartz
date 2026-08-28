using Quartz.Features.MainMenu;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PageMainMenu {
    public static void Create(RectTransform parent) {
        MenuTweaks.EnsureConf();
        MenuTweaksSettings conf = MenuTweaks.Conf;
        MenuTweaksSettings def = new();
        RectTransform content = Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent);
        var mainMenuSec = GenerateUI.FlatSection(content.transform, "Main Menu");
        GenerateUI.ToggleTip(
            mainMenuSec.Body,
            def.DisableMenuMusic,
            conf.DisableMenuMusic,
            v => { conf.DisableMenuMusic = v; MenuTweaks.Save(); },
            "Disable Menu Music",
            "tw_menumusic",
            "Mutes the theme song on the title and island-select screens. Takes effect immediately; gameplay music is untouched."
        );
        GenerateUI.ToggleTip(
            mainMenuSec.Body,
            def.MenuBpmEnabled,
            conf.MenuBpmEnabled,
            v => { conf.MenuBpmEnabled = v; MenuTweaks.Save(); },
            "Custom Menu BPM",
            "tw_menubpm",
            "Sets the menu rabbit's two speeds to the BPMs below instead of the default 1x / 2x. Re-open the menu to apply."
        );
        UISlider slowBpm = GenerateUI.Slider(
            GenerateUI.Row(mainMenuSec.Body),
            def.MenuSlowBpm, 30f, 600f, conf.MenuSlowBpm,
            Mathf.Round, v => conf.MenuSlowBpm = v,
            v => { conf.MenuSlowBpm = v; MenuTweaks.Save(); },
            "Slow BPM", "tw_menuslowbpm"
        );
        slowBpm.Format = "0";
        UISlider highBpm = GenerateUI.Slider(
            GenerateUI.Row(mainMenuSec.Body),
            def.MenuHighBpm, 30f, 600f, conf.MenuHighBpm,
            Mathf.Round, v => conf.MenuHighBpm = v,
            v => { conf.MenuHighBpm = v; MenuTweaks.Save(); },
            "High BPM", "tw_menuhighbpm"
        );
        highBpm.Format = "0";
    }
}

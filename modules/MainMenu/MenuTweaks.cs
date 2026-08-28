using Quartz.Core;
using Quartz.IO;
using UnityEngine;
using Quartz.Compat.Game;
namespace Quartz.Features.MainMenu;
public static partial class MenuTweaks {
    public static SettingsFile<MenuTweaksSettings> ConfMgr { get; private set; }
    public static MenuTweaksSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() {
        if(ConfMgr != null) return;
        ConfMgr = new SettingsFile<MenuTweaksSettings>(
            System.IO.Path.Combine(MainCore.Paths.RootPath, "MainMenu.json"));
        if(!ConfMgr.Load() && LegacyTweaks.Adopt(ConfMgr.Data)) ConfMgr.Save();
    }
    public static void Save() => ConfMgr?.RequestSave();
    private static bool Enabled {
        get {
            EnsureConf();
            return MainCore.IsModEnabled;
        }
    }
    private static bool ShouldDisableMenuMusic => Enabled && Conf.DisableMenuMusic;
    private static bool menuFast;
    private static bool ShouldCustomMenuBpm => Enabled && Conf.MenuBpmEnabled;
    internal static void ApplyInitialMenuBpm() {
        if(!ShouldCustomMenuBpm) return;
        scrConductor cond = ADOBase.conductor;
        if(cond == null || cond.bpm <= 0f) return;
        menuFast = false;
        SetAllPlayerSpeed(Conf.MenuSlowBpm / cond.bpm);
        SetMenuSong2(false);
    }
    internal static bool HandleMenuBpmToggle(scrFloor floor) {
        if(!ShouldCustomMenuBpm || floor == null) return false;
        scrConductor cond = ADOBase.conductor;
        if(cond == null || cond.bpm <= 0f) return false;
        menuFast = !menuFast;
        SetAllPlayerSpeed((menuFast ? Conf.MenuHighBpm : Conf.MenuSlowBpm) / cond.bpm);
        floor.floorIcon = menuFast ? FloorIcon.Snail : FloorIcon.Rabbit;
        floor.UpdateIconSprite();
        SetMenuSong2(menuFast);
        return true;
    }
    private static void SetAllPlayerSpeed(double speed) {
        try {
            GameApi.SetPlanetSpeed(speed);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void SetMenuSong2(bool fast) {
        try {
            AudioSource song2 = ADOBase.conductor?.song2;
            if(song2 != null) song2.volume = fast ? 0.7f : 0f;
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static scrConductor lastMuteConductor;
    private static bool lastMuteTarget;
    internal static void ApplyMenuMusicMute(scrConductor conductor) {
        if(conductor == null) return;
        bool target;
        try { target = ShouldDisableMenuMusic && ADOBase.isLevelSelect; }
        catch(Exception e) { Diag.Ignore(e); return; }
        if(!target && !lastMuteTarget && ReferenceEquals(conductor, lastMuteConductor)) return;
        try {
            if(conductor.song != null && conductor.song.mute != target) conductor.song.mute = target;
            if(conductor.song2 != null && conductor.song2.mute != target) conductor.song2.mute = target;
            lastMuteConductor = conductor;
            lastMuteTarget = target;
        } catch(Exception e) { Diag.Ignore(e); }
    }
}

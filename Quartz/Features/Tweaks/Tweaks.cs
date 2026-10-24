using System.Reflection;
using Quartz.Core;
using Quartz.Game.Stats;
using Quartz.IO;
using Quartz.Compat.Game;
namespace Quartz.Features.Tweaks;
public static partial class Tweaks {
    public static SettingsFile<TweaksSettings> ConfMgr { get; private set; }
    public static TweaksSettings Conf => ConfMgr?.Data;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<TweaksSettings>.Loaded("Tweaks.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static bool Enabled {
        get {
            EnsureConf();
            return MainCore.IsModEnabled;
        }
    }
    private static bool ShouldDisableAutoPause => Enabled && Conf.DisableAutoPause;
    private static bool ShouldBlockMouseWheelScroll =>
        Enabled && Conf.BlockMouseWheelScrollWhilePlaying && GameStats.InGame;
    private static bool IsSafePauseCallSite() {
        try {
            System.Diagnostics.StackTrace st = new(2, false);
            for(int i = 0; i < st.FrameCount; i++) {
                MethodBase m = st.GetFrame(i).GetMethod();
                if(m == null) continue;
                Type dt = m.DeclaringType;
                if(dt == null) continue;
                string name = m.Name;
                if(dt == typeof(scnGame) && name == "ResetScene") return true;
                if(dt == typeof(scnEditor)) {
                    if(name == "SwitchToEditMode" || name == "TogglePause" ||
                        name == "ResetScene" || name == "SwitchToPlayMode" ||
                        name == "PauseIfUnpaused")
                        return true;
                }
                if(dt == typeof(PauseMenu)) return true;
            }
        } catch(Exception e) { Diag.Ignore(e); }
        return false;
    }
    private static void ResetEditorPlayModePauseState() {
        try {
            scnEditor editor = ADOBase.editor;
            if(editor == null) return;
            GameApi.ClearEditorPlayModePause(editor);
            if(editor.buttonAuto != null) editor.buttonAuto.interactable = true;
        } catch(Exception e) { Diag.Ignore(e); }
    }
}

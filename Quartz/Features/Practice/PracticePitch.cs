using ADOFAI;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Practice;
public static class PracticePitch {
    public static int LevelPitch {
        get {
            LevelData data = Current();
            if(data == null) return 100;
            try { return data.pitch; } catch { return 100; }
        }
    }
    public static void SetLevelPitch(int percent) {
        LevelData data = Current();
        if(data == null) return;
        int target = PracticeSettings.ClampPitch(percent);
        try {
            if(data.pitch == target) return;
            data.songSettings["pitch"] = target;
        } catch(Exception e) {
            MainCore.Log.Wrn("[Practice] could not set the level pitch: " + e.Message);
            return;
        }
        RefreshInspector();
    }
    private static LevelData Current() {
        try {
            scnEditor editor = ADOBase.editor;
            if(editor != null && editor.levelData != null) return editor.levelData;
        } catch { }
        try {
            scnGame game = ADOBase.customLevel;
            if(game != null && game.levelData != null) return game.levelData;
        } catch { }
        return null;
    }
    private static void RefreshInspector() {
        try {
            scnEditor editor = scnEditor.instance;
            InspectorPanel settings = editor == null ? null : editor.settingsPanel;
            if(settings == null || !settings.isActiveAndEnabled) return;
            settings.ShowPanel(settings.selectedEventType, settings.cacheEventIndex);
        } catch { }
    }
}

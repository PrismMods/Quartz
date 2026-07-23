using HarmonyLib;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.IO;
using UnityEngine;
namespace Quartz.Features.Practice;
public static class PracticeDifficulty {
    public static SettingsFile<PracticeSettings> ConfMgr { get; private set; }
    public static PracticeSettings Conf => ConfMgr?.Data;
    public const int DifficultyCount = 3;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<PracticeSettings>.Loaded("Practice.json");
    public static void Save() => ConfMgr?.Save();
    public static int CurrentDifficulty {
        get {
            try { return Mathf.Clamp((int)GCS.difficulty, 0, DifficultyCount - 1); } catch { return -1; }
        }
    }
    public static int CurrentPitch => PracticePitch.LevelPitch;
    public static string DifficultyName(int index) => index switch {
        0 => MainCore.Tr.Get("PRACTICE_DIFF_LENIENT", "Lenient"),
        1 => MainCore.Tr.Get("PRACTICE_DIFF_NORMAL", "Normal"),
        2 => MainCore.Tr.Get("PRACTICE_DIFF_STRICT", "Strict"),
        _ => "?",
    };
    public static void Apply(PracticeBinding binding) {
        if(binding == null) return;
        if(binding.SetsDifficulty) SetDifficulty(binding.Difficulty);
        if(binding.SetsSpeed) PracticePitch.SetLevelPitch(binding.Pitch);
    }
    private static int pendingDifficulty = -1;
    public static int PendingDifficulty => pendingDifficulty;
    private static bool InRun {
        get {
            try {
                scrController controller = scrController.instance;
                return controller != null && controller.gameworld && controller.currentSeqID > 1;
            } catch { return false; }
        }
    }
    public static void SetDifficulty(int index) {
        index = Mathf.Clamp(index, 0, DifficultyCount - 1);
        if(InRun) {
            pendingDifficulty = index == CurrentDifficulty ? -1 : index;
            return;
        }
        pendingDifficulty = -1;
        ApplyDifficulty(index);
    }
    internal static void FlushPendingDifficulty() {
        if(pendingDifficulty < 0 || InRun) return;
        int index = pendingDifficulty;
        pendingDifficulty = -1;
        ApplyDifficulty(index);
    }
    private static void ApplyDifficulty(int index) {
        try {
            GCS.difficulty = (Difficulty)index;
            Refl.Invoke(Refl.Method(Refl.Type("Persistence"), "SetDefaultDifficulty", 1), null, (Difficulty)index);
        } catch(Exception e) {
            MainCore.Log.Wrn("[Practice] could not set the difficulty: " + e.Message);
            return;
        }
        RefreshDifficultyDisplay();
    }
    private static void RefreshDifficultyDisplay() {
        try {
            scnEditor editor = ADOBase.editor;
            if(editor != null && editor.editorDifficultySelector != null)
                Traverse.Create(editor.editorDifficultySelector).Method("UpdateDifficultyDisplay").GetValue();
        } catch { }
        try {
            scrUIController ui = scrUIController.instance;
            if(ui == null) return;
            Traverse difficultyIndex = Traverse.Create(ui).Field("currentDifficultyIndex");
            if(difficultyIndex.FieldExists()) {
                difficultyIndex.SetValue(CurrentDifficulty);
                Traverse.Create(ui).Method("UpdateDifficultyUI").GetValue();
            }
        } catch { }
    }
}

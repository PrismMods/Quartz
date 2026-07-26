using HarmonyLib;
namespace Quartz.Features.Editor;
public static partial class EditorFeature {
    private static bool IsEditorEditMode() {
        scnEditor editor = scnEditor.instance;
        return editor != null && !editor.playMode;
    }
    [HarmonyPatch(typeof(scrUIController), "ShowDifficultyContainer")]
    private static class GameplayDifficultyShowPatch {
        private static bool Prefix(scrUIController __instance) {
            if(!IsEditorEditMode()) return true;
            Quartz.Compat.Game.GameUi.HideDifficultyContainer(__instance);
            return false;
        }
    }
    [HarmonyPatch(typeof(scrUIController), "DifficultyArrowPressed")]
    private static class GameplayDifficultyArrowPatch {
        private static bool Prefix(scrUIController __instance) {
            if(!IsEditorEditMode()) return true;
            Quartz.Compat.Game.GameUi.HideDifficultyContainer(__instance);
            return false;
        }
    }
}

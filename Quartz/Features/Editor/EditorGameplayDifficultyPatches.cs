using HarmonyLib;
using UiHiderFeature = Quartz.Features.UiHider.UiHider;

namespace Quartz.Features.Editor;

public static partial class EditorFeature {
    // True only when the level editor is open AND editing (not playing). Custom
    // levels PLAY inside the "scnEditor" scene in playMode, so scnEditor.instance
    // != null is NOT enough to tell "editing" from "playing a custom level": the
    // difficulty (strict/normal/lenient) selector is shown at the start of every
    // level via scrPressToStart, and blocking it whenever scnEditor exists killed
    // the difficulty arrows during normal custom-level play. Gate on !playMode so
    // suppression only applies to genuine edit mode. (Same playMode idiom used by
    // EditorFloorReadout / EditorBgaMod.)
    private static bool IsEditorEditMode() {
        scnEditor editor = scnEditor.instance;
        return editor != null && !editor.playMode;
    }

    [HarmonyPatch(typeof(scrUIController), "ShowDifficultyContainer")]
    private static class GameplayDifficultyShowPatch {
        private static bool Prefix(scrUIController __instance) {
            if(!IsEditorEditMode()) return true;
            UiHiderFeature.HideGameplayDifficultyContainer(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(scrUIController), "DifficultyArrowPressed")]
    private static class GameplayDifficultyArrowPatch {
        private static bool Prefix(scrUIController __instance) {
            if(!IsEditorEditMode()) return true;
            UiHiderFeature.HideGameplayDifficultyContainer(__instance);
            return false;
        }
    }
}

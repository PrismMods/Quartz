using ADOFAI;
using ADOFAI.Editor.Actions;
using HarmonyLib;
namespace Quartz.Features.Interop;
// Core patch, so it stays on after Quartz or Effect Remover is turned off with a stripped level still open.
internal static class StrippedLevelSaveGuard {
    [HarmonyPatch(typeof(LevelData), "Decode")]
    private static class DecodePatch {
        private static void Prefix() => EffectRemover.EffectRemover.LevelStripped = false;
    }
    [HarmonyPatch(typeof(SaveLevelEditorAction), "Execute")]
    private static class SavePatch {
        private static bool Prefix() => !EffectRemover.EffectRemover.StrippedSaveBlocked;
    }
}

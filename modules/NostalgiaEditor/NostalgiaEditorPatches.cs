using DG.Tweening;
using HarmonyLib;
using Object = UnityEngine.Object;
using Quartz.Compat.Game;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static Quartz.Features.Nostalgia.Nostalgia;
namespace Quartz.Features.Nostalgia;
public static class NostalgiaEditorPatches {
    [HarmonyPatch(typeof(scnEditor), "Play")]
    private static class Space360TilePatch {
        private static bool Prefix(scnEditor __instance) {
            if(ShouldSpace360Tile
               && !Input.GetKeyDown(KeyCode.P)
               && !Input.GetKey(KeyCode.LeftShift)
               && !Input.GetKey(KeyCode.RightShift)
               && Input.GetKeyDown(KeyCode.Space)) {
                if(__instance.SelectionIsSingle()) {
                    scrFloor floor = __instance.selectedFloors[0];
                    object floatDir = GameApi.RotateDirection(
                        GameApi.RotateDirection(floor.floatDirection, true), true);
                    object stringDir = GameApi.RotateDirection(
                        GameApi.RotateDirection(floor.stringDirection, true), true);
                    Traverse.Create(__instance).Method(
                        "CreateFloorWithCharOrAngle",
                        floatDir, stringDir, true, true).GetValue();
                }
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(scrShowIfDebug), "Update")]
    private static class WeakAutoPatch {
        private static void Prefix(out bool __state) {
            __state = RDC.useOldAuto;
            if(ShouldWeakAuto) RDC.useOldAuto = false;
        }
        private static void Postfix(bool __state) {
            if(RDC.useOldAuto != __state) RDC.useOldAuto = __state;
        }
    }
    [HarmonyPatch(typeof(scnEditor), "get_highBPM")]
    private static class WhiteAutoHighBpmPatch {
        private static void Postfix(ref bool __result) {
            if(ShouldWhiteAuto) __result = false;
        }
    }
    [HarmonyPatch(typeof(scnGame), "ResetScene")]
    private static class WhiteAutoResetPatch {
        private static void Postfix() {
            if(ShouldWhiteAuto && scnEditor.instance != null) scnEditor.instance.autoFailed = false;
        }
    }
    [HarmonyPatch(typeof(scnEditor), "Awake")]
    private static class LegacyEditorButtonsPatch {
        private static void Postfix() {
            ChangeEditorButtons(Enabled && Conf.LegacyEditorButtonsPositions);
            RemoveShadowAddOutline(Enabled && Conf.LegacyEditorButtonsDesigns);
        }
    }
    [HarmonyPatch(typeof(RDString), "GetWithCheck")]
    private static class LegacyTextsPatch {
        private static void Postfix(ref string __result) {
            if(!ShouldLegacyTexts) return;
            switch(__result) {
                case "눈폭풍":
                    __result = "눈폭충";
                    break;
                case "세피아":
                    __result = "소피아";
                    break;
                case "작곡가":
                    __result = "아티스트";
                    break;
            }
        }
    }
    [HarmonyPatch]
    private static class HideDifficultyApplyPatch {
        private static IEnumerable<MethodBase> TargetMethods() => new[] {
            AccessTools.Method(typeof(scnEditor), "Start"),
            AccessTools.Method(typeof(scnEditor), "SwitchToEditMode"),
            AccessTools.Method(typeof(scrUIController), "ShowDifficultyContainer"),
            AccessTools.Method(typeof(EditorDifficultySelector), "SetChangeable"),
        }.Where(m => m != null);
        private static void Postfix() {
            if(ShouldHideDifficulty) ToggleDifficulty(false);
        }
    }
    [HarmonyPatch]
    private static class HideDifficultyCancelPatch {
        private static IEnumerable<MethodBase> TargetMethods() => new[] {
            AccessTools.Method(typeof(EditorDifficultySelector), "ToggleDifficulty"),
            AccessTools.Method(typeof(scrUIController), "DifficultyArrowPressed"),
        }.Where(m => m != null);
        private static bool Prefix() => !ShouldHideDifficulty;
    }
    [HarmonyPatch]
    private static class HideNoFailApplyPatch {
        private static IEnumerable<MethodBase> TargetMethods() => new[] {
            AccessTools.Method(typeof(scnEditor), "SwitchToEditMode"),
            AccessTools.Method(typeof(scnEditor), "Start"),
        }.Where(m => m != null);
        private static void Postfix() {
            if(ShouldHideNoFail) ToggleNoFail(false);
        }
    }
    [HarmonyPatch(typeof(scnEditor), "ToggleNoFail")]
    private static class EditorToggleNoFailPatch {
        private static bool Prefix() => !ShouldHideNoFail;
    }
    [HarmonyPatch(typeof(scnEditor), "LateUpdate")]
    private static class EditorReHidePatch {
        private static void Postfix(scnEditor __instance) {
            if(ShouldHideDifficulty
               && __instance.editorDifficultySelector != null
               && __instance.editorDifficultySelector.gameObject.activeSelf) {
                ToggleDifficulty(false);
            }
            if(ShouldHideNoFail
               && __instance.buttonNoFail != null
               && __instance.buttonNoFail.gameObject.activeSelf) {
                ToggleNoFail(false);
            }
            RepositionDifficulty();
        }
    }
    [HarmonyPatch]
    private static class ClsToggleNoFailPatch {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(scnCLS), "ToggleNoFail")
            ?? AccessTools.Method(
                typeof(ADOBase).Assembly.GetType("OptionsPanelsCLS"), "ToggleNoFail");
        private static bool Prepare(MethodBase original) => TargetMethod() != null;
        private static bool Prefix() => !ShouldHideNoFail;
    }
}

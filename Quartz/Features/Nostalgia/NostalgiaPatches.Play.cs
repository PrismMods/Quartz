using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using HarmonyLib;
using UnityEngine;
namespace Quartz.Features.Nostalgia;
public static partial class Nostalgia {
    [HarmonyPatch(typeof(scrController), "OnLandOnPortal")]
    private static class NoResultPatch {
        private static void Postfix(scrController __instance) {
            if(__instance.gameworld && ShouldNoResult) {
                __instance.txtCongrats.text = string.Empty;
                scrUIController.instance.txtResults.gameObject.SetActive(false);
                __instance.txtAllStrictClear.text = string.Empty;
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
    [HarmonyPatch(typeof(scrController), "Fail2_Update")]
    private static class OldPracticeModePatch {
        private static bool Prefix() {
            if(ShouldOldPracticeMode
               && scrController.instance.practiceAvailable
               && !GCS.practiceMode
               && Input.GetKeyDown(KeyCode.P)) {
                scrController.instance.SetPracticeMode(true);
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(scrFloor), "UpdateIconSprite")]
    private static class ShowSpeedChangePatch {
        private static void Prefix(scrFloor __instance) {
            if(!ShouldShowSmallSpeedChange || scrLevelMaker.instance == null) return;
            switch(__instance.floorIcon) {
                case FloorIcon.Rabbit:
                case FloorIcon.DoubleRabbit:
                case FloorIcon.Snail:
                case FloorIcon.DoubleSnail:
                case FloorIcon.AnimatedRabbit:
                case FloorIcon.AnimatedDoubleRabbit:
                case FloorIcon.AnimatedSnail:
                case FloorIcon.AnimatedDoubleSnail:
                case FloorIcon.SameSpeed:
                    break;
                default:
                    return;
            }
            float prevSpeed = __instance.seqID > 0
                ? scrLevelMaker.instance.listFloors[__instance.seqID - 1].speed
                : 1f;
            float speedDifference = (__instance.speed - prevSpeed) / prevSpeed;
            bool detail = Conf.ShowDetailSpeedChange;
            if(detail && Mathf.Abs(speedDifference) <= Conf.MinBpmToShowSpeedChange) {
                __instance.floorIcon = FloorIcon.SameSpeed;
            } else if(!detail && Mathf.Abs(speedDifference) == 0) {
                __instance.floorIcon = FloorIcon.SameSpeed;
            } else {
                __instance.floorIcon = (speedDifference > 0f)
                    ? ((Mathf.Abs(speedDifference) < 1.05f) ? FloorIcon.Rabbit : FloorIcon.DoubleRabbit)
                    : ((1f - Mathf.Abs(speedDifference) > 0.45f) ? FloorIcon.Snail : FloorIcon.DoubleSnail);
            }
        }
    }
    [HarmonyPatch]
    private static class LegacyFlashPatch {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(scrPlayer), "OnDamage")
            ?? AccessTools.Method(typeof(scrController), "OnDamage");
        private static bool Prepare() => TargetMethod() != null;
        private static void Postfix() {
            if(!ShouldLegacyFlash) return;
            scrFlash.FlashEx(Color.red.WithAlpha(0.5f), Color.clear, 0.6f);
        }
    }
    [HarmonyPatch(typeof(scrHitTextMesh), "Show")]
    private static class NoJudgeAnimationPatch {
        private static void Postfix(scrHitTextMesh __instance) {
            if(!ShouldNoJudgeAnimation) return;
            __instance.transform.DOKill();
            __instance.transform.localRotation = scrCamera.instance.transform.rotation;
        }
    }
    [HarmonyPatch(typeof(scrHitTextManager), "ShowHitText")]
    private static class LateJudgementPatch {
        private static bool Prepare() => AccessTools.Method(typeof(scrHitTextManager), "ShowHitText") != null;
        private static void Postfix(scrHitTextManager __instance, HitMargin hitMargin, scrPlanet planet) {
            if(!ShouldLateJudgement) return;
            switch(hitMargin) {
                case HitMargin.TooEarly:
                case HitMargin.TooLate:
                case HitMargin.FailMiss:
                case HitMargin.FailOverload:
                    return;
            }
            try {
                scrFloor other = planet?.other?.currfloor;
                if(other == null) return;
                Vector3 pos = other.transform.position;
                pos.y += 1f;
                EnsureHitTextAccessors();
                var cached = cachedHitTextsRef != null ? cachedHitTextsRef(__instance) : null;
                if(cached == null || !cached.TryGetValue(hitMargin, out scrHitTextMesh[] arr)) return;
                scrHitTextMesh newest = null;
                int best = int.MinValue;
                foreach(scrHitTextMesh m in arr) {
                    if(m == null || m.dead) continue;
                    int fs = hitTextMeshFrameShownRef != null ? hitTextMeshFrameShownRef(m) : 0;
                    if(fs >= best) {
                        best = fs;
                        newest = m;
                    }
                }
                if(newest != null) {
                    if(hitTextMeshTextPosRef != null) hitTextMeshTextPosRef(newest) = pos;
                    newest.transform.position = pos;
                }
            } catch { }
        }
    }
    private static bool hitTextAccessorsResolved;
    private static AccessTools.FieldRef<scrHitTextManager, Dictionary<HitMargin, scrHitTextMesh[]>> cachedHitTextsRef;
    private static AccessTools.FieldRef<scrHitTextMesh, int> hitTextMeshFrameShownRef;
    private static AccessTools.FieldRef<scrHitTextMesh, Vector3> hitTextMeshTextPosRef;
    private static void EnsureHitTextAccessors() {
        if(hitTextAccessorsResolved) return;
        hitTextAccessorsResolved = true;
        try {
            cachedHitTextsRef = AccessTools.FieldRefAccess<scrHitTextManager, Dictionary<HitMargin, scrHitTextMesh[]>>("cachedHitTexts");
        } catch { }
        try {
            hitTextMeshFrameShownRef = AccessTools.FieldRefAccess<scrHitTextMesh, int>("frameShown");
        } catch { }
        try {
            hitTextMeshTextPosRef = AccessTools.FieldRefAccess<scrHitTextMesh, Vector3>("textPos");
        } catch { }
    }
}

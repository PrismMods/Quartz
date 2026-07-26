using HarmonyLib;
using System.Reflection;
using UnityEngine;
namespace Quartz.Features.UiHider;
public static partial class UiHider {
    [HarmonyPatch(typeof(scrHitTextMesh), "Show")]
    private static class JudgmentTextShowPatch {
        private static void Prefix(ref Vector3 position) {
            if(ShouldHideJudgementText()) position = HiddenPosition;
        }
    }
    [HarmonyPatch]
    private static class MissIndicatorPatch {
        private static bool Prepare() => AccessTools.TypeByName("scrMissIndicator") != null;
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("scrMissIndicator"), "Awake");
        private static void Postfix(object __instance) {
            if(!ShouldHideMissIndicators()) return;
            if(__instance is Component component) component.transform.position = HiddenPosition;
        }
    }
    [HarmonyPatch(typeof(scrShowIfDebug), "Update")]
    private static class HideAutoplayTextPatch {
        private static bool prevAuto;
        private static void Prefix() {
            prevAuto = RDC.auto;
            if(ShouldHideOtto()) RDC.auto = false;
        }
        private static void Postfix() {
            RDC.auto = prevAuto;
        }
    }
    [HarmonyPatch(typeof(scnEditor), "SwitchToEditMode")]
    private static class ReapplyOnEditModePatch {
        private static void Postfix() => ApplyNow();
    }
    [HarmonyPatch]
    private static class HideShortcutHintPatch {
        private static bool Prepare() => ShortcutTextType != null;
        private static MethodBase TargetMethod() => AccessTools.Method(ShortcutTextType, "SetText");
        private static void Postfix(object __instance) {
            if(!ShouldHideShortcutHints()) return;
            if(__instance is Component component
                && component.GetComponent<UnityEngine.UI.Text>() is UnityEngine.UI.Text label
                && label.text.Length > 0) label.text = "";
        }
    }
    private static readonly Type ShortcutTextType = AccessTools.TypeByName("scrShortcutText");
    internal static void RefreshShortcutHints() {
        if(ShortcutTextType == null) return;
        MethodInfo setText = AccessTools.Method(ShortcutTextType, "SetText");
        if(setText == null) return;
        try {
            foreach(UnityEngine.Object found in Resources.FindObjectsOfTypeAll(ShortcutTextType)) {
                try { setText.Invoke(found, null); } catch { }
            }
        } catch { }
    }
    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class ClearCachesOnSceneChangePatch {
        private static void Postfix() => ClearMemberValueCache();
    }
    private static class HideResultTextAndFlashPatches {
        private static bool shouldIgnoreFlashOnce;
        [HarmonyPatch(typeof(scrController), "OnLandOnPortal")]
        private static class HideResultTextPatch {
            private static void Prefix() {
                if(ShouldHideLastFloorFlash()) shouldIgnoreFlashOnce = true;
            }
            private static void Postfix(scrController __instance) {
                if(!ShouldHideResult()) return;
                SetMemberInactive(__instance, "txtCongrats");
                SetMemberInactive(__instance, "txtResults");
                SetMemberInactive(__instance, "txtAllStrictClear");
            }
        }
        [HarmonyPatch]
        private static class HideLastFloorFlashPatch {
            private static bool Prepare() => AccessTools.TypeByName("scrFlash") != null;
            private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("scrFlash"), "Flash");
            private static bool Prefix(object[] __args) {
                if(!shouldIgnoreFlashOnce || !TryGetFlashColor(__args, out Color colorStart) || !IsLastFloorFlashColor(colorStart)) return true;
                shouldIgnoreFlashOnce = false;
                return false;
            }
        }
    }
    private static class HideHitErrorMeterPatches {
        private static void HideErrorMeter() {
            if(!ShouldHideHitErrorMeter()) return;
            scrController controller = scrController.instance;
            if(controller == null || !controller.gameworld) return;
            GameObject errorMeter = GetGameObject(GetMemberValueCached(controller, "errorMeter"));
            if(errorMeter != null && errorMeter.activeSelf) errorMeter.SetActive(false);
        }
        [HarmonyPatch(typeof(scrController), "paused", MethodType.Setter)]
        private static class PausedSetterPatch {
            private static void Postfix() => HideErrorMeter();
        }
        [HarmonyPatch(typeof(scrPlanet), "MoveToNextFloor")]
        private static class MoveToNextFloorPatch {
            private static void Postfix() => HideErrorMeter();
        }
        [HarmonyPatch]
        private static class TaroCutscenePatch {
            private static bool Prepare() => AccessTools.TypeByName("TaroCutsceneScript") != null;
            private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("TaroCutsceneScript"), "DisplayText");
            private static void Postfix() => HideErrorMeter();
        }
    }
    private static void SetMemberInactive(object owner, string memberName) {
        GameObject gameObject = GetGameObject(GetMemberValue(owner, memberName));
        if(gameObject != null) gameObject.SetActive(false);
    }
    private static bool IsLastFloorFlashColor(Color color) {
        return Mathf.Abs(color.r - 1f) < 0.001f
            && Mathf.Abs(color.g - 1f) < 0.001f
            && Mathf.Abs(color.b - 1f) < 0.001f
            && Mathf.Abs(color.a - 0.4f) < 0.001f;
    }
    private static bool TryGetFlashColor(object[] args, out Color color) {
        color = default;
        if(args == null || args.Length == 0 || args[0] is not Color c) return false;
        color = c;
        return true;
    }
}

using DG.Tweening;
using HarmonyLib;
using Object = UnityEngine.Object;
using Quartz.Compat.Game;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static Quartz.Features.Nostalgia.Nostalgia;
using Quartz.Core;
namespace Quartz.Features.Nostalgia;
public static class NostalgiaVisualsPatches {
    private static readonly HashSet<int> twirlArrowFloors = [];
    [HarmonyPatch(typeof(scrController), "OnLandOnPortal")]
    private static class NoResultPatch {
        private static void Postfix(scrController __instance) {
            if(__instance.gameworld && ShouldNoResult) {
                __instance.txtCongrats.text = string.Empty;
                GameApi.ResultsReadout(scrUIController.instance)?.SetActive(false);
                __instance.txtAllStrictClear.text = string.Empty;
            }
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
        private static MethodBase TargetMethod() => GameApi.OnDamageTarget;
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
            __instance.transform.localRotation = GameApi.Camera.transform.rotation;
        }
    }
    private static scrPlanet legacyHitPlanet;
    [HarmonyPatch]
    private static class LegacySwitchChosenPatch {
        private static bool Prepare() => GameApi.SwitchChosenTarget != null && GameApi.LegacyShowHitTextTarget != null;
        private static MethodBase TargetMethod() => GameApi.SwitchChosenTarget;
        private static void Prefix(scrPlanet __instance) => legacyHitPlanet = __instance;
        private static void Finalizer() => legacyHitPlanet = null;
    }
    [HarmonyPatch]
    private static class LegacyLateJudgementPatch {
        private static bool Prepare() => GameApi.LegacyShowHitTextTarget != null;
        private static MethodBase TargetMethod() => GameApi.LegacyShowHitTextTarget;
        private static void Prefix(HitMargin hitMargin, ref Vector3 position) {
            if(!ShouldLateJudgement || legacyHitPlanet == null) return;
            switch(hitMargin) {
                case HitMargin.TooEarly:
                case HitMargin.TooLate:
                case HitMargin.FailMiss:
                case HitMargin.FailOverload:
                    return;
            }
            try {
                scrFloor other = legacyHitPlanet.other?.currfloor;
                if(other == null) return;
                Vector3 pos = other.transform.position;
                pos.y += 1f;
                position = pos;
            } catch(Exception e) { Diag.Ignore(e); }
        }
    }
    [HarmonyPatch]
    private static class LateJudgementPatch {
        private static MethodBase TargetMethod() => GameApi.ShowHitTextTarget;
        private static bool Prepare() => TargetMethod() != null;
        private static void Postfix(object __instance, HitMargin hitMargin, scrPlanet planet) {
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
                var cached = cachedHitTextsField?.GetValue(__instance) as Dictionary<HitMargin, scrHitTextMesh[]>;
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
            } catch(Exception e) { Diag.Ignore(e); }
        }
    }
    private static bool hitTextAccessorsResolved;
    private static FieldInfo cachedHitTextsField;
    private static AccessTools.FieldRef<scrHitTextMesh, int> hitTextMeshFrameShownRef;
    private static AccessTools.FieldRef<scrHitTextMesh, Vector3> hitTextMeshTextPosRef;
    private static void EnsureHitTextAccessors() {
        if(hitTextAccessorsResolved) return;
        hitTextAccessorsResolved = true;
        try {
            cachedHitTextsField = AccessTools.Field(GameApi.ShowHitTextTarget?.DeclaringType, "cachedHitTexts");
        } catch(Exception e) { Diag.Ignore(e); }
        try {
            hitTextMeshFrameShownRef = AccessTools.FieldRefAccess<scrHitTextMesh, int>("frameShown");
        } catch(Exception e) { Diag.Ignore(e); }
        try {
            hitTextMeshTextPosRef = AccessTools.FieldRefAccess<scrHitTextMesh, Vector3>("textPos");
        } catch(Exception e) { Diag.Ignore(e); }
    }
    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class TwirlSceneResetPatch {
        private static void Postfix() => twirlArrowFloors.Clear();
    }
    [HarmonyPatch(typeof(scrFloor), "UpdateIconSprite")]
    private static class LegacyTwirlPatch {
        private static void Postfix(scrFloor __instance) {
            bool hadArrows = twirlArrowFloors.Count > 0
                && twirlArrowFloors.Remove(__instance.GetInstanceID());
            if(!ShouldLegacyTwirl && !hadArrows) return;
            if(hadArrows) {
                Object.DestroyImmediate(__instance.transform.Find("arrow_renderer")?.gameObject);
                Object.DestroyImmediate(__instance.transform.Find("arrow_outline_renderer")?.gameObject);
            }
            if(!ShouldLegacyTwirl
               || __instance.isportal
               || (__instance.floorIcon != FloorIcon.Swirl && __instance.floorIcon != FloorIcon.SwirlCW)) {
                return;
            }
            NostalgiaImages.EnsureLoaded();
            float num = (float)scrMisc.GetAngleMoved(
                (float)__instance.entryangle, (float)__instance.exitangle, !__instance.isCCW);
            if(Mathf.Abs(num) <= 1E-06f && !__instance.midSpin) num = Mathf.PI * 2;
            __instance.SetIconSprite(__instance.isCCW ? NostalgiaImages.SwirlCcw : NostalgiaImages.SwirlCw);
            __instance.SetIconFlipped(false);
            float num2 = 0f;
            if(__instance.floorRenderer is FloorSpriteRenderer) {
                float num3 = (ADOBase.lm?.lm2?.BigTiles ?? false) ? Mathf.PI / -2 : Mathf.PI / 2;
                num2 = (float)(((scrMisc.mod((float)(__instance.exitangle - __instance.entryangle), Mathf.PI * 2) <= Mathf.PI)
                    ? __instance.entryangle : __instance.exitangle) - num3);
            }
            float num4 = -(float)__instance.entryangle + Mathf.PI / 2
                - num / 2f * (__instance.isCCW ? -1 : 1) - Mathf.PI / 2 + num2;
            __instance.SetIconAngle((__instance.floorRenderer is FloorSpriteRenderer) ? num4 : (-num4));
            __instance.SetIconOutlineSprite(__instance.isCCW ? NostalgiaImages.SwirlCcwOutline : NostalgiaImages.SwirlCwOutline);
            if(Conf.TwirlWithoutArrow) return;
            Renderer iconRef = (Renderer)__instance.iconsprite ?? __instance.floorRenderer.renderer;
            twirlArrowFloors.Add(__instance.GetInstanceID());
            GameObject arrowObj = new();
            arrowObj.transform.parent = __instance.transform;
            TwirlRenderer arrow = arrowObj.AddComponent<TwirlRenderer>();
            arrow.outline = false;
            arrow.floor = __instance;
            arrow.sr.sprite = __instance.isCCW ? NostalgiaImages.ArrowCcw : NostalgiaImages.ArrowCw;
            arrow.sr.sortingLayerID = iconRef.sortingLayerID;
            arrow.sr.sortingLayerName = iconRef.sortingLayerName;
            arrow.name = "arrow_renderer";
            Vector3 localPos = new(
                0.3f * Mathf.Cos(num4 + 90 * Mathf.Deg2Rad),
                0.3f * Mathf.Sin(num4 + 90 * Mathf.Deg2Rad), 0f);
            arrow.transform.localPosition = localPos;
            arrow.transform.localEulerAngles = new Vector3(0f, 0f, num4 * Mathf.Rad2Deg);
            bool forward = num < Mathf.PI - Mathf.Pow(10f, -6f);
            arrow.sr.color = forward ? Color.red : Color.blue;
            if(__instance.outline) {
                GameObject arrowOutlineObj = new();
                arrowOutlineObj.transform.parent = __instance.transform;
                TwirlRenderer arrowOutline = arrowOutlineObj.AddComponent<TwirlRenderer>();
                arrowOutline.outline = true;
                arrowOutline.floor = __instance;
                arrowOutline.sr.sprite = __instance.isCCW ? NostalgiaImages.ArrowCcwOutline : NostalgiaImages.ArrowCwOutline;
                arrowOutline.sr.sortingLayerID = iconRef.sortingLayerID;
                arrowOutline.sr.sortingLayerName = iconRef.sortingLayerName;
                arrowOutline.name = "arrow_outline_renderer";
                arrowOutline.transform.localPosition = localPos;
                arrowOutline.transform.localEulerAngles = new Vector3(0f, 0f, num4 * Mathf.Rad2Deg);
            }
        }
    }
}
public sealed class TwirlRenderer : MonoBehaviour {
    public SpriteRenderer sr;
    public scrFloor floor;
    public bool outline;
    private void Awake() => sr = gameObject.GetOrAddComponent<SpriteRenderer>();
    private void LateUpdate() {
        if(floor == null) return;
        if(floor.floorIcon != FloorIcon.Swirl && floor.floorIcon != FloorIcon.SwirlCW) {
            Destroy(gameObject);
            return;
        }
        Renderer iconRef = (Renderer)floor.iconsprite ?? floor.floorRenderer.renderer;
        int order = iconRef.sortingOrder + (outline ? 1 : 2);
        if(sr.sortingOrder != order) sr.sortingOrder = order;
        Vector3 scale = floor.transform.localScale;
        Vector3 current = sr.transform.localScale;
        if(current.x != scale.x || current.y != scale.y || current.z != scale.z)
            sr.transform.localScale = scale;
        float alpha = floor.floorRenderer.color.a * floor.opacity;
        if(sr.color.a != alpha) sr.SetAlpha(alpha);
    }
}

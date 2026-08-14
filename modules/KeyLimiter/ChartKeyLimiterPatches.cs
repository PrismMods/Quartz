using System.Reflection;
using System.Reflection.Emit;
using ADOFAI;
using HarmonyLib;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.KeyLimiter;
internal static class EnumToStringPatch {
    private static Harmony harmony;
    private static MethodInfo target;
    private static bool applied;
    internal static void Bind(Harmony instance) => harmony = instance;
    internal static bool Apply() {
        if(applied) return true;
        if(harmony == null) return false;
        try {
            target ??= AccessTools.Method(typeof(Enum), nameof(ToString), Type.EmptyTypes);
            if(target == null) {
                MainCore.Log.Err("[KeyLimiter] Enum.ToString() is missing, chart key limiter events stay off");
                return false;
            }
            harmony.CreateProcessor(target)
                .AddPrefix(new HarmonyMethod(AccessTools.Method(typeof(EnumToStringPatch), nameof(Prefix))))
                .Patch();
            applied = true;
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[KeyLimiter] could not name the chart event type: {e}");
            return false;
        }
    }
    internal static void Remove() {
        if(!applied) return;
        applied = false;
        try {
            harmony.Unpatch(target, HarmonyPatchType.Prefix, harmony.Id);
        } catch(Exception e) {
            Diag.Warn(e, "unpatching the chart key limiter event name hook");
        }
    }
    private static bool Prefix(Enum __instance, ref string __result) {
        if(__instance is not LevelEventType type) return true;
        if((int)type != ChartKeyLimiter.EventTypeId) return true;
        __result = ChartKeyLimiter.EventName;
        return false;
    }
}
public static partial class ChartKeyLimiter {
    [HarmonyPatch(typeof(ADOStartup), nameof(ADOStartup.SetupLevelEventsInfo))]
    private static class SetupLevelEventsInfoPatch {
        private static void Postfix() => Apply();
    }
    [HarmonyPatch(typeof(scnGame), nameof(scnGame.ApplyEvent))]
    private static class ApplyEventPatch {
        private static bool Prefix(
            LevelEvent evnt,
            float bpm,
            float pitch,
            List<scrFloor> floors,
            float offset,
            int? customFloorID,
            ref ffxPlusBase __result
        ) {
            if(evnt == null || (int)evnt.eventType != EventTypeId) return true;
            if(!Registered) return false;
            try {
                int floorId = customFloorID ?? evnt.floor;
                if(floors == null || floorId < 0 || floorId >= floors.Count) return false;
                scrFloor floor = floors[floorId];
                if(floor == null) return false;
                FfxKeyLimiter effect = floor.gameObject.AddComponent<FfxKeyLimiter>();
                effect.floorID = floorId;
                effect.floors = floors;
                effect.crotchet = (float)(60.0 / ((double)bpm * pitch * floor.speed));
                effect.Decode(evnt);
                effect.SetStartTime(bpm, offset);
                effect.sourceLevelEvent = evnt;
                floor.plusEffects.Add(effect);
                __result = effect;
            } catch(Exception e) {
                MainCore.Log.Err($"[KeyLimiter] could not apply a chart key limiter event: {e}");
            }
            return false;
        }
    }
    [HarmonyPatch(typeof(scnGame), nameof(scnGame.Play), [typeof(int), typeof(bool)])]
    private static class PlayPatch {
        private static void Prefix() => ChartKeyLimiterState.Instance.Clear();
    }
    [HarmonyPatch(typeof(scrPlanet), "SwitchChosen")]
    private static class SwitchChosenPatch {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> code = [.. instructions];
            MethodInfo getHitMargin = AccessTools.Method(typeof(scrMisc), nameof(scrMisc.GetHitMargin));
            MethodInfo validate = AccessTools.Method(typeof(SwitchChosenPatch), nameof(Validate));
            if(getHitMargin == null || validate == null) {
                MainCore.Log.Err("[KeyLimiter] scrMisc.GetHitMargin is gone, chart key limits will not fire");
                return code;
            }
            for(int i = 0; i < code.Count; i++) {
                if(code[i].opcode != OpCodes.Call) continue;
                if(!ReferenceEquals(code[i].operand, getHitMargin)) continue;
                code.Insert(i + 1, new CodeInstruction(OpCodes.Call, validate));
                code.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_0));
                return code;
            }
            MainCore.Log.Err("[KeyLimiter] no GetHitMargin call in scrPlanet.SwitchChosen, chart key limits will not fire");
            return code;
        }
        private static HitMargin Validate(HitMargin margin, scrPlanet planet) {
            try {
                if(!ChartKeyLimiterState.Instance.Active) return margin;
                if(planet == null || planet.player == null || RDC.auto) return margin;
                int playerId = planet.player.playerID;
                foreach(AnyKeyCode pressed in RDInput.GetMainPressKeys()) {
                    KeyCode key = Resolve(pressed);
                    if(key == KeyCode.None) continue;
                    if(ChartKeyLimiterState.Instance.IsKeyValid(playerId, key)) continue;
                    if(ChartKeyLimiterState.Instance.OnInvalidPress(ref margin))
                        planet.player.Die(false, true, ChartKeyLimiterState.Instance.Message, false);
                }
            } catch(Exception e) {
                Diag.Warn(e, "checking a hit against the chart key limiter");
            }
            return margin;
        }
        private static KeyCode Resolve(AnyKeyCode pressed) {
            object value = pressed.value;
            if(value is KeyCode key) return key;
            if(value is AsyncKeyCode asyncKey)
                return KeyLimiter.HookKeyToPhysicalUnityKey(asyncKey.key, asyncKey.label);
            return KeyCode.None;
        }
    }
    [HarmonyPatch(typeof(LevelData), nameof(LevelData.Encode))]
    private static class EncodePatch {
        private static void Prefix(LevelData __instance) {
            if(!Registered || __instance?.levelSettings == null || __instance.levelEvents == null) return;
            try {
                bool used = false;
                foreach(LevelEvent evnt in __instance.levelEvents) {
                    if(evnt == null || (int)evnt.eventType != EventTypeId) continue;
                    used = true;
                    break;
                }
                if(__instance.levelSettings["requiredMods"] is not object[] mods) return;
                bool listed = Array.IndexOf(mods, RequiredModName) >= 0;
                if(listed == used) return;
                HashSet<object> updated = [.. mods];
                if(used) updated.Add(RequiredModName);
                else updated.Remove(RequiredModName);
                __instance.levelSettings["requiredMods"] = updated.ToArray();
            } catch(Exception e) {
                Diag.Warn(e, "recording the chart key limiter in the level's required mods");
            }
        }
    }
    [HarmonyPatch]
    private static class EditorEventSpritesPatch {
        private static MethodBase TargetMethod() {
            string name = AccessTools.GetMethodNames(typeof(scnEditor))
                .FirstOrDefault(n => n.StartsWith("<Start>g__LoadLevelEventSprites", StringComparison.Ordinal));
            return name == null ? null : AccessTools.Method(typeof(scnEditor), name);
        }
        private static bool Prepare(MethodBase original) => original != null || TargetMethod() != null;
        private static void Postfix() => RegisterEditorIcon();
    }
    [HarmonyPatch]
    private static class ParseLevelEventTypePatch {
        private static MethodBase TargetMethod() {
            MethodInfo generic = typeof(RDUtils)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "ParseEnum" && m.IsGenericMethodDefinition);
            return generic?.MakeGenericMethod(typeof(LevelEventType));
        }
        private static bool Prepare(MethodBase original) => original != null || TargetMethod() != null;
        private static bool Prefix(string str, ref LevelEventType __result) {
            if(!Registered || str != EventName) return true;
            __result = EventType;
            return false;
        }
    }
    [HarmonyPatch]
    private static class EditorStringsPatch {
        private static MethodBase TargetMethod() => AccessTools.Method(
            typeof(RDString), nameof(RDString.GetWithCheck),
            [typeof(string), typeof(bool).MakeByRefType(), typeof(Dictionary<string, object>)]);
        private static bool Prepare(MethodBase original) => original != null || TargetMethod() != null;
        private static void Postfix(string key, ref bool exists, ref string __result) {
            if(exists || !Registered) return;
            if(!ChartKeyLimiterStrings.TryGet(key, out string value)) return;
            __result = value;
            exists = true;
        }
    }
}

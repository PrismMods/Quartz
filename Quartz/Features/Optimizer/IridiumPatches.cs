using System.Runtime.CompilerServices;
using ADOFAI;
using HarmonyLib;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Optimizer;
public static class IridiumPatches {
    private sealed class ParticleState {
        public Vector2 Scale;
        public float SimulationSpeed;
        public float Pitch;
        public bool Primed;
    }
    private static readonly ConditionalWeakTable<scrParticleDecoration, ParticleState> particleStates = [];
    private static readonly AccessTools.FieldRef<scnGame, Camera> GameCamera = Bind<Camera>("camera");
    private static readonly AccessTools.FieldRef<scnGame, int> GameStartFrame = BindInt("startFrame");
    private static float lastOrthoSize;
    private static float lastAspect;
    private static bool screenScaleValid;
    private static AccessTools.FieldRef<scnGame, T> Bind<T>(string name) where T : class {
        try {
            return AccessTools.FieldRefAccess<scnGame, T>(name);
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    private static AccessTools.FieldRef<scnGame, int> BindInt(string name) {
        try {
            return AccessTools.FieldRefAccess<scnGame, int>(name);
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    internal static void InvalidateScreenScale() => screenScaleValid = false;
    internal static void ApplyParticleCulling() {
        ParticleSystemCullingMode mode = Optimizer.PauseOffscreenParticlesActive
            ? ParticleSystemCullingMode.Pause
            : ParticleSystemCullingMode.Automatic;
        scrParticleDecoration[] decorations;
        try {
            decorations = UnityEngine.Object.FindObjectsByType<scrParticleDecoration>(FindObjectsSortMode.None);
        } catch(Exception e) {
            Diag.Ignore(e);
            return;
        }
        if(decorations == null) return;
        for(int i = 0; i < decorations.Length; i++) {
            scrParticleDecoration decoration = decorations[i];
            if(decoration == null) continue;
            try {
                ParticleSystem system = decoration.particleSystem;
                if(system == null) continue;
                ParticleSystem.MainModule main = system.main;
                main.cullingMode = mode;
            } catch(Exception e) { Diag.Ignore(e); }
        }
    }
    [HarmonyPatch(typeof(scnGame), "Update")]
    private static class CacheScreenScalePatch {
        private static bool Prefix(scnGame __instance) {
            if(!Optimizer.CacheScreenScaleActive) return true;
            if(GameCamera == null || GameStartFrame == null) return true;
            if(IsLevelLoadFrame(__instance)) return true;
            Camera cam = GameCamera(__instance);
            if(cam == null) return true;
            float ortho = cam.orthographicSize;
            float aspect = cam.aspect;
            if(screenScaleValid
            && Mathf.Approximately(ortho, lastOrthoSize)
            && Mathf.Approximately(aspect, lastAspect))
                return false;
            lastOrthoSize = ortho;
            lastAspect = aspect;
            screenScaleValid = true;
            return true;
        }
        private static bool IsLevelLoadFrame(scnGame game) {
            try {
                if(GCS.customLevelPaths == null && !ADOBase.isInternalLevel) return false;
                if(ADOBase.isLevelEditor) return false;
                return Time.frameCount - GameStartFrame(game) == 3;
            } catch(Exception e) {
                Diag.Ignore(e);
                return true;
            }
        }
    }
    [HarmonyPatch(typeof(scrParticleDecoration), "Update")]
    private static class SkipIdleParticlesPatch {
        private static bool Prefix(scrParticleDecoration __instance) {
            if(!Optimizer.SkipIdleParticlesActive || __instance == null) return true;
            if(__instance.atStart || __instance.autoPlay) return true;
            bool inEditor;
            try { inEditor = ADOBase.isLevelEditor; } catch(Exception e) { Diag.Ignore(e); return true; }
            if(inEditor) return true;
            float pitch;
            try { pitch = ADOBase.conductor.song.pitch; } catch(Exception e) { Diag.Ignore(e); return true; }
            ParticleState state = particleStates.GetValue(__instance, static _ => new ParticleState());
            if(state.Primed
            && state.Scale == __instance.scale
            && state.SimulationSpeed == __instance.simulationSpeed
            && state.Pitch == pitch)
                return false;
            state.Scale = __instance.scale;
            state.SimulationSpeed = __instance.simulationSpeed;
            state.Pitch = pitch;
            state.Primed = true;
            return true;
        }
    }
    [HarmonyPatch(typeof(scrParticleDecoration), nameof(scrParticleDecoration.ResetParticle))]
    private static class ResetParticlePatch {
        private static void Postfix(scrParticleDecoration __instance) {
            if(__instance == null) return;
            if(particleStates.TryGetValue(__instance, out ParticleState state)) state.Primed = false;
            if(!Optimizer.PauseOffscreenParticlesActive) return;
            try {
                ParticleSystem system = __instance.particleSystem;
                if(system == null) return;
                ParticleSystem.MainModule main = system.main;
                main.cullingMode = ParticleSystemCullingMode.Pause;
            } catch(Exception e) { Diag.Ignore(e); }
        }
    }
}

using HarmonyLib;
using Quartz.Core;
using UnityEngine.SceneManagement;

namespace Quartz.Addons;

// Curated gameplay events for addons. Each event invokes its subscribers one
// by one with a per-subscriber try/catch, so one faulty addon can't break the
// others or the game loop. The Harmony patches below ride HarmonyService's
// PatchAll like every feature's patches; the SceneManager subscription is
// owned by AddonService (Initialize/Dispose).
//
// Addons that need anything beyond these can patch the game directly via
// Context.Harmony — the whole game assembly is referenced by addon compiles.
public static class AddonEvents {
    // A run started (custom level Play or built-in level controller start).
    public static event Action LevelStart;

    // The player left the level scene.
    public static event Action LevelEnd;

    // A judgement was registered. Fires for every hit, including Auto.
    public static event Action<HitMargin> Hit;

    // A Unity scene finished loading.
    public static event Action<Scene> SceneLoaded;

    // Mod master switch changed (true = enabled).
    public static event Action<bool> ModEnabledChanged;

    internal static void RaiseSceneLoaded(Scene scene) => SafeRaise(SceneLoaded, scene);
    internal static void RaiseModEnabledChanged(bool enabled) => SafeRaise(ModEnabledChanged, enabled);

    // UMM in-process reload support: a torn-down runtime must not leave stale
    // addon handlers subscribed to these static events.
    internal static void Clear() {
        LevelStart = null;
        LevelEnd = null;
        Hit = null;
        SceneLoaded = null;
        ModEnabledChanged = null;
    }

    private static void SafeRaise(Action evt) {
        if(evt == null) return;
        foreach(Action handler in evt.GetInvocationList()) {
            try {
                handler();
            } catch(Exception e) {
                MainCore.Log.Err($"[Addons] event handler threw: {e}");
            }
        }
    }

    private static void SafeRaise<T>(Action<T> evt, T arg) {
        if(evt == null) return;
        foreach(Action<T> handler in evt.GetInvocationList()) {
            try {
                handler(arg);
            } catch(Exception e) {
                MainCore.Log.Err($"[Addons] event handler threw: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(scnGame), "Play")]
    private static class RunStartPatch {
        private static void Postfix() {
            if(!MainCore.IsModEnabled) return;
            SafeRaise(LevelStart);
        }
    }

    // Built-in/official levels never instantiate scnGame (same reasoning as
    // Combo's ResetOnControllerStartPatch), so their run start is the
    // controller's Start in a gameworld scene.
    [HarmonyPatch(typeof(scrController), "Start")]
    private static class ControllerStartPatch {
        private static void Postfix(scrController __instance) {
            if(!MainCore.IsModEnabled) return;
            if(__instance.gameworld) SafeRaise(LevelStart);
        }
    }

    [HarmonyPatch(typeof(scrController), "StartLoadingScene")]
    private static class RunExitPatch {
        private static void Postfix() {
            if(!MainCore.IsModEnabled) return;
            SafeRaise(LevelEnd);
        }
    }

    [HarmonyPatch(typeof(scrMarginTracker), "AddHit", typeof(HitMargin))]
    private static class AddHitPatch {
        private static void Postfix(HitMargin hit) {
            if(!MainCore.IsModEnabled) return;
            SafeRaise(Hit, hit);
        }
    }
}

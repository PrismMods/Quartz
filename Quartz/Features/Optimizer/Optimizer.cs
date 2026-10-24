using System.Diagnostics;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.Game.Stats;
using Quartz.IO;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
namespace Quartz.Features.Optimizer;
public static class Optimizer {
    public static SettingsFile<OptimizerSettings> ConfMgr { get; private set; }
    public static OptimizerSettings Conf => ConfMgr?.Data;
    public static readonly IRuntimeTick Ticker = new TickImpl();
    private const long GCReserveBytes = 64L * 1024 * 1024;
    private static bool defaultsCaptured;
    private static bool defaultRunInBackground;
    private static ProcessPriorityClass defaultPriority = ProcessPriorityClass.Normal;
    private static bool gcDeferred;
    private static bool usingNoGcRegion;
    private static bool loggedGcStrategy;
    private static bool optimizerActive;
    private static bool smoothGcActive;
    private static bool leakGuardActive;
    private static bool collectOnLevelLoadActive;
    private static bool fastBloomActive;
    private static bool skipNoOpScreenFiltersActive;
    private static bool cacheScreenScaleActive;
    private static bool skipIdleParticlesActive;
    private static bool pauseOffscreenParticlesActive;
    private static bool renderAllHitSoundsActive;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<OptimizerSettings>.Loaded("Optimizer.json");
    public static void Save() => ConfMgr?.RequestSave();
    public static void Initialize() {
        EnsureConf();
        CaptureDefaults();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        HitSoundRenderer.EnsureSceneHook();
        Apply();
    }
    private static void CaptureDefaults() {
        if(defaultsCaptured) return;
        defaultRunInBackground = Application.runInBackground;
        try {
            defaultPriority = Process.GetCurrentProcess().PriorityClass;
        } catch(Exception e) {
            Diag.Ignore(e);
            defaultPriority = ProcessPriorityClass.Normal;
        }
        defaultsCaptured = true;
    }
    public static void Apply() {
        EnsureConf();
        CaptureDefaults();
        bool on = MainCore.IsModEnabled;
        CacheRuntimeFlags(on);
        Application.runInBackground = on && Conf.RunInBackground
            ? true
            : defaultRunInBackground;
        SetPriority(on && Conf.BoostProcessPriority
            ? ProcessPriorityClass.AboveNormal
            : defaultPriority);
        if(gcDeferred && !(smoothGcActive && GameStats.InGame)) ResumeGC();
        TMPTextShadow.UnderlayOffsetScale = Conf.ShadowUnderlayOffsetScale;
        TMPTextShadow.UseMaterialUnderlay = on && Conf.LightTextShadows;
        if(!renderAllHitSoundsActive) HitSoundRenderer.StopAll("disabled");
        IridiumPatches.InvalidateScreenScale();
        IridiumPatches.ApplyParticleCulling();
    }
    public static void Restore() {
        CacheRuntimeFlags(false);
        if(gcDeferred) ResumeGC();
        Application.runInBackground = defaultRunInBackground;
        SetPriority(defaultPriority);
    }
    internal static bool LeakGuardActive => leakGuardActive;
    internal static bool FastBloomActive => fastBloomActive;
    internal static bool SkipNoOpScreenFiltersActive => skipNoOpScreenFiltersActive;
    internal static bool CacheScreenScaleActive => cacheScreenScaleActive;
    internal static bool SkipIdleParticlesActive => skipIdleParticlesActive;
    internal static bool PauseOffscreenParticlesActive => pauseOffscreenParticlesActive;
    internal static bool RenderAllHitSoundsActive => renderAllHitSoundsActive;
    private static void CacheRuntimeFlags(bool on) {
        optimizerActive = on;
        smoothGcActive = on && Conf != null && Conf.SmoothGC;
        leakGuardActive = on && Conf != null && Conf.LeakGuard;
        collectOnLevelLoadActive = on && Conf != null && Conf.CollectOnLevelLoad;
        fastBloomActive = on && Conf != null && Conf.FastBloom;
        skipNoOpScreenFiltersActive = on && Conf != null && Conf.SkipNoOpScreenFilters;
        cacheScreenScaleActive = on && Conf != null && Conf.CacheScreenScale;
        skipIdleParticlesActive = on && Conf != null && Conf.SkipIdleParticles;
        pauseOffscreenParticlesActive = on && Conf != null && Conf.PauseOffscreenParticles;
        renderAllHitSoundsActive = on && Conf != null && Conf.RenderAllHitSounds;
    }
    private static void SetPriority(ProcessPriorityClass priority) {
        try {
            Process proc = Process.GetCurrentProcess();
            if(proc.PriorityClass != priority) proc.PriorityClass = priority;
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        IridiumPatches.InvalidateScreenScale();
        if(!optimizerActive) return;
        if(leakGuardActive) LeakGuardPatches.SweepStaticCaches();
        if(collectOnLevelLoadActive) GC.Collect();
    }
    public static void Unhook() => SceneManager.sceneLoaded -= OnSceneLoaded;
    private static void Tick() {
        bool wantDefer = smoothGcActive && GameStats.InGame;
        if(wantDefer != gcDeferred) {
            if(wantDefer) DeferGC(); else ResumeGC();
        }
        HitSoundRenderer.Pump();
    }
    private static void DeferGC() {
        if(GarbageCollector.isIncremental) {
            usingNoGcRegion = false;
            gcDeferred = true;
            LogGcStrategyOnce("incremental GC present — leaving collection enabled.");
            return;
        }
        try {
            usingNoGcRegion = TryReserveNoGcBudget();
            gcDeferred = true;
            LogGcStrategyOnce(usingNoGcRegion
                ? "no-GC region reserved (auto-recovers when the budget is spent)."
                : "no-GC region unavailable — leaving collection enabled.");
        } catch {
            usingNoGcRegion = false;
            gcDeferred = true;
            LogGcStrategyOnce("no-GC region unsupported — leaving collection enabled.");
        }
    }
    private static bool TryReserveNoGcBudget() => GC.TryStartNoGCRegion(GCReserveBytes, true);
    private static void ResumeGC() {
        if(usingNoGcRegion) {
            try { GC.EndNoGCRegion(); } catch(Exception e) { Diag.Ignore(e); }
            try { GC.Collect(); } catch(Exception e) { Diag.Ignore(e); }
        }
        usingNoGcRegion = false;
        gcDeferred = false;
    }
    private static void LogGcStrategyOnce(string detail) {
        if(loggedGcStrategy) return;
        loggedGcStrategy = true;
        MainCore.Log.Msg("[Optimizer] SmoothGC: " + detail);
    }
    private sealed class TickImpl : IRuntimeTick {
        public void Tick() => Optimizer.Tick();
    }
}

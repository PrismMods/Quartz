using System.Diagnostics;
using Quartz.Compat.Interface;
using Quartz.Core;
using Quartz.Features.Status;
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
    private const long GCSafetyBytes = 96L * 1024 * 1024;
    private static bool defaultsCaptured;
    private static bool defaultRunInBackground;
    private static ProcessPriorityClass defaultPriority = ProcessPriorityClass.Normal;
    private static bool gcDeferred;
    private static long heapAtDefer;
    private static bool usingManualDefer;
    private static bool loggedGcStrategy;
    public static void EnsureConf() => ConfMgr ??= SettingsFile<OptimizerSettings>.Loaded("Optimizer.json");
    public static void Save() => ConfMgr?.RequestSave();
    private static bool Active {
        get {
            EnsureConf();
            return MainCore.IsModEnabled;
        }
    }
    public static void Initialize() {
        EnsureConf();
        CaptureDefaults();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }
    private static void CaptureDefaults() {
        if(defaultsCaptured) return;
        defaultRunInBackground = Application.runInBackground;
        try {
            defaultPriority = Process.GetCurrentProcess().PriorityClass;
        } catch {
            defaultPriority = ProcessPriorityClass.Normal;
        }
        defaultsCaptured = true;
    }
    public static void Apply() {
        EnsureConf();
        CaptureDefaults();
        bool on = MainCore.IsModEnabled;
        Application.runInBackground = on && Conf.RunInBackground
            ? true
            : defaultRunInBackground;
        SetPriority(on && Conf.BoostProcessPriority
            ? ProcessPriorityClass.AboveNormal
            : defaultPriority);
        if(gcDeferred && !(on && Conf.SmoothGC && GameStats.InGame)) ResumeGC();
        TMPTextShadow.UnderlayOffsetScale = Conf.ShadowUnderlayOffsetScale;
        TMPTextShadow.UseMaterialUnderlay = on && Conf.LightTextShadows;
    }
    public static void Restore() {
        if(gcDeferred) ResumeGC();
        Application.runInBackground = defaultRunInBackground;
        SetPriority(defaultPriority);
    }
    internal static bool FastBloomActive {
        get {
            EnsureConf();
            return MainCore.IsModEnabled && Conf != null && Conf.FastBloom;
        }
    }
    internal static bool SkipNoOpScreenFiltersActive {
        get {
            EnsureConf();
            return MainCore.IsModEnabled && Conf != null && Conf.SkipNoOpScreenFilters;
        }
    }
    private static void SetPriority(ProcessPriorityClass priority) {
        try {
            Process proc = Process.GetCurrentProcess();
            if(proc.PriorityClass != priority) proc.PriorityClass = priority;
        } catch {
        }
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if(Active && Conf.CollectOnLevelLoad) GC.Collect();
    }
    public static void Unhook() => SceneManager.sceneLoaded -= OnSceneLoaded;
    private static void Tick() {
        bool wantDefer = Active && Conf.SmoothGC && GameStats.InGame;
        if(wantDefer != gcDeferred) {
            if(wantDefer) {
                DeferGC();
            } else {
                ResumeGC();
            }
            return;
        }
        if(gcDeferred && usingManualDefer
            && GC.GetTotalMemory(false) - heapAtDefer > GCSafetyBytes) {
            GC.Collect();
            heapAtDefer = GC.GetTotalMemory(false);
        }
    }
    private static void DeferGC() {
        try {
            if(!loggedGcStrategy) {
                loggedGcStrategy = true;
                MainCore.Log.Msg(GarbageCollector.isIncremental
                    ? "[Optimizer] SmoothGC: incremental GC present — leaving collection enabled (no Manual defer)."
                    : "[Optimizer] SmoothGC: no incremental GC — deferring via Manual mode (96MB heap cap).");
            }
            if(GarbageCollector.isIncremental) {
                usingManualDefer = false;
                gcDeferred = true;
                return;
            }
            GarbageCollector.GCMode = GarbageCollector.Mode.Manual;
            usingManualDefer = true;
            gcDeferred = true;
            heapAtDefer = GC.GetTotalMemory(false);
        } catch {
            gcDeferred = false;
            usingManualDefer = false;
        }
    }
    private static void ResumeGC() {
        try {
            if(usingManualDefer) {
                GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                GC.Collect();
            }
        } catch { }
        usingManualDefer = false;
        gcDeferred = false;
    }
    private sealed class TickImpl : IRuntimeTick {
        public void Tick() => Optimizer.Tick();
    }
}

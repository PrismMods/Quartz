#if QUARTZ_UMM
using Quartz.Core;
using Quartz.Compat.Interface;
using UnityModManagerNet;
namespace Quartz;
public sealed class LoaderUmm : IQuartzHost, IQuartzLogger {
    private enum LoaderState { Waiting, Initializing, Active, Failed, Stopped }
    private static LoaderUmm instance;
    private readonly UnityModManager.ModEntry.ModLogger logger;
    private readonly string modPath;
    private readonly string dataPath;
    private readonly object lease = FlavorGuard.CreateLease();
    private LoaderState state;
    private bool conflictLogged;
    private bool? pendingToggle;
    private LoaderUmm(UnityModManager.ModEntry modEntry) {
        logger = modEntry.Logger;
        modPath = modEntry.Path;
        dataPath = Path.Combine(modPath, "UserData");
    }
    public static bool Load(UnityModManager.ModEntry modEntry) {
        if(instance != null) {
            modEntry.Logger.Error("Another instance of this Quartz loader is already registered.");
            return false;
        }
        LoaderUmm owner = new(modEntry);
        instance = owner;
        modEntry.OnUpdate = owner.OnUpdate;
        modEntry.OnToggle = owner.OnToggle;
        modEntry.OnUnload = owner.OnUnload;
        return true;
    }
    private void OnUpdate(UnityModManager.ModEntry entry, float _) {
        if(!ReferenceEquals(instance, this)) return;
        if(state == LoaderState.Active) {
            MainCore.Tick();
            return;
        }
        if(state != LoaderState.Waiting) return;
        if(!FlavorGuard.TryClaim(lease, out string conflict)) {
            if(!conflictLogged) {
                conflictLogged = true;
                entry.Logger.Error(FlavorGuard.Message(conflict));
            }
            return;
        }
        state = LoaderState.Initializing;
        try {
            MainCore.Initialize(this);
            state = LoaderState.Active;
            if(pendingToggle is bool enabled) {
                pendingToggle = null;
                try { MainCore.SetModEnabled(enabled); }
                catch(System.Exception e) {
                    entry.Logger.Error($"Quartz could not apply the queued enabled state: {e}");
                }
            }
        } catch(System.Exception e) {
            state = LoaderState.Failed;
            FlavorGuard.Release(lease);
            entry.Logger.Error($"Quartz failed to initialize: {e}");
            entry.OnUpdate = null;
        }
    }
    private bool OnToggle(UnityModManager.ModEntry _, bool value) {
        if(state == LoaderState.Active) {
            MainCore.SetModEnabled(value);
            return true;
        }
        if(state is LoaderState.Waiting or LoaderState.Initializing) {
            pendingToggle = value;
            return true;
        }
        return false;
    }
    private bool OnUnload(UnityModManager.ModEntry entry) {
        bool ownsInstance = ReferenceEquals(instance, this);
        bool dispose = ownsInstance && state == LoaderState.Active;
        state = LoaderState.Stopped;
        try {
            if(dispose) MainCore.Dispose();
        } finally {
            FlavorGuard.Release(lease);
            entry.OnUpdate = null;
            entry.OnToggle = null;
            entry.OnUnload = null;
            if(ownsInstance) instance = null;
        }
        return true;
    }
    public IQuartzLogger QuartzLogger => this;
    public string QuartzFilePath => dataPath;
    public string ModsPath => modPath;
    public string UserLibsPath => modPath;
    public bool SupportsSelfUpdate => true;
    public string UpdateAssetName => Info.Name + "Umm.zip";
    public string UpdateExtractRoot =>
        Directory.GetParent(modPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.FullName;
    public void QuartzMsg(string msg) => logger.Log(msg);
    public void QuartzWrn(string msg) => logger.Warning(msg);
    public void QuartzErr(string msg) => logger.Error(msg);
}
#endif

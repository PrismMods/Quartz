
#if !QUARTZ_UMM
using MelonLoader;
using MelonLoader.Utils;
using Quartz.Core;
using Quartz.Compat.Interface;
using Quartz.Update;
namespace Quartz;
// The MelonLoader entry, invoked reflectively by Quartz.Bootstrap (the thin
// MelonMod that lives in Mods/ and picks which versioned runtime to load).
// This assembly is no longer a MelonMod itself: the bootstrap forwards
// OnUpdate/OnDeinitialize into Tick/Unload, and Load throwing is the signal
// it uses to roll a bad update back.
public static class PayloadBridge {
    private enum LoaderState { New, Waiting, Initializing, Active, Failed, Stopped }
    private static readonly HostMl Host = new();
    private static readonly object lease = FlavorGuard.CreateLease();
    private static LoaderState state;
    private static bool conflictLogged;
    public static void Load() {
        if(state != LoaderState.New) return;
        state = LoaderState.Waiting;
        TryActivate(true);
    }
    private static void TryActivate(bool throwOnFailure) {
        if(state != LoaderState.Waiting) return;
        if(!FlavorGuard.TryClaim(lease, out string conflict)) {
            if(!conflictLogged) {
                conflictLogged = true;
                MelonLogger.Error(FlavorGuard.Message(conflict));
            }
            return;
        }
        state = LoaderState.Initializing;
        try {
            MainCore.Initialize(Host);
            state = LoaderState.Active;
            UpdateLaunchPrefs.Bind(Path.Combine(Host.QuartzFilePath, "Runtime"));
        } catch(Exception e) {
            state = LoaderState.Failed;
            FlavorGuard.Release(lease);
            if(throwOnFailure) throw;
            MelonLogger.Error($"Quartz failed to initialize: {e}");
        }
    }
    public static void Tick() {
        if(state == LoaderState.Active) {
            MainCore.Tick();
            return;
        }
        if(state == LoaderState.Waiting) TryActivate(false);
    }
    public static void Unload() {
        if(state == LoaderState.Stopped) return;
        bool dispose = state == LoaderState.Active;
        state = LoaderState.Stopped;
        try {
            if(dispose) MainCore.Dispose();
        } finally { FlavorGuard.Release(lease); }
    }
}
internal sealed class HostMl : IQuartzHost, IQuartzLogger {
    public IQuartzLogger QuartzLogger => this;
    // Info.Name, not a literal: the KeyViewer flavour ships as its own mod and must
    // not read or write the full build's UserData/Quartz folder.
    public string QuartzFilePath => Path.Combine(MelonEnvironment.UserDataDirectory, Info.Name);
    public string ModsPath => MelonEnvironment.ModsDirectory;
    public string UserLibsPath => MelonEnvironment.UserLibsDirectory;
    public bool SupportsSelfUpdate => true;
    public string UpdateAssetName => Info.Name + ".zip";
    public string UpdateExtractRoot => Directory.GetParent(MelonEnvironment.ModsDirectory)?.FullName;
    public void QuartzMsg(string msg) => MelonLogger.Msg(msg);
    public void QuartzWrn(string msg) => MelonLogger.Warning(msg);
    public void QuartzErr(string msg) => MelonLogger.Error(msg);
}
#endif

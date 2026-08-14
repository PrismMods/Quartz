
#if !QUARTZ_UMM
using MelonLoader;
using MelonLoader.Utils;
using Quartz;
using Quartz.Core;
using Quartz.Compat.Interface;
[assembly: MelonInfo(typeof(Loader), Info.Name, Info.Version, Info.Author, Info.GithubLink)]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]
[assembly: HarmonyDontPatchAll]
namespace Quartz;
public class Loader : MelonMod, IQuartzHost, IQuartzLogger {
    private enum LoaderState { New, Waiting, Initializing, Active, Failed, Stopped }
    private readonly object lease = FlavorGuard.CreateLease();
    private LoaderState state;
    private bool conflictLogged;
    public IQuartzLogger QuartzLogger => this;
    // Info.Name, not a literal: the KeyViewer flavour ships as its own mod and must
    // not read or write the full build's UserData/Quartz folder.
    public string QuartzFilePath => Path.Combine(MelonEnvironment.UserDataDirectory, Info.Name);
    public string ModsPath => MelonEnvironment.ModsDirectory;
    public string UserLibsPath => MelonEnvironment.UserLibsDirectory;
    public bool SupportsSelfUpdate => true;
    public string UpdateAssetName => Info.Name + ".zip";
    public string UpdateExtractRoot => Directory.GetParent(MelonEnvironment.ModsDirectory)?.FullName;
    public override void OnInitializeMelon() {
        if(state != LoaderState.New) return;
        state = LoaderState.Waiting;
        TryActivate(true);
    }
    private void TryActivate(bool throwOnFailure) {
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
            MainCore.Initialize(this);
            state = LoaderState.Active;
        } catch(Exception e) {
            state = LoaderState.Failed;
            FlavorGuard.Release(lease);
            if(throwOnFailure) throw;
            MelonLogger.Error($"Quartz failed to initialize: {e}");
        }
    }
    public override void OnDeinitializeMelon() {
        if(state == LoaderState.Stopped) return;
        bool dispose = state == LoaderState.Active;
        state = LoaderState.Stopped;
        try {
            if(dispose) MainCore.Dispose();
        } finally { FlavorGuard.Release(lease); }
    }
    public override void OnUpdate() {
        if(state == LoaderState.Active) {
            MainCore.Tick();
            return;
        }
        if(state == LoaderState.Waiting) TryActivate(false);
    }
    public void QuartzMsg(string msg) => MelonLogger.Msg(msg);
    public void QuartzWrn(string msg) => MelonLogger.Warning(msg);
    public void QuartzErr(string msg) => MelonLogger.Error(msg);
}
#endif

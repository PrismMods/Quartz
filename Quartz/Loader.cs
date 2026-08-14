
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
#if QUARTZ_KEYVIEWER
        if(FlavorGuard.FullQuartzLoaded()) {
            MelonLogger.Error(FlavorGuard.Message);
            return;
        }
#endif
        MainCore.Initialize(this);
    }
    public override void OnDeinitializeMelon() => MainCore.Dispose();
    public override void OnUpdate() => MainCore.Tick();
    public void QuartzMsg(string msg) => MelonLogger.Msg(msg);
    public void QuartzWrn(string msg) => MelonLogger.Warning(msg);
    public void QuartzErr(string msg) => MelonLogger.Error(msg);
}
#endif

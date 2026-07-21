#if QUARTZ_UMM
using Quartz.Core;
using Quartz.Compat.Interface;
using UnityModManagerNet;
namespace Quartz;
public sealed class LoaderUmm : IQuartzHost, IQuartzLogger {
    private static LoaderUmm instance;
    private readonly UnityModManager.ModEntry.ModLogger logger;
    private readonly string modPath;
    private readonly string dataPath;
    private LoaderUmm(UnityModManager.ModEntry modEntry) {
        logger = modEntry.Logger;
        modPath = modEntry.Path;
        dataPath = Path.Combine(modPath, "UserData");
    }
    public static bool Load(UnityModManager.ModEntry modEntry) {
        if(instance != null) return true;
        instance = new LoaderUmm(modEntry);
        bool initialized = false;
        modEntry.OnUpdate = (entry, _) => {
            if(initialized) {
                MainCore.Tick();
                return;
            }
            initialized = true;
            try {
                MainCore.Initialize(instance);
            } catch(System.Exception e) {
                entry.Logger.Error($"Quartz failed to initialize: {e}");
                entry.OnUpdate = null;
                instance = null;
            }
        };
        modEntry.OnToggle = (_, value) => {
            MainCore.SetModEnabled(value);
            return true;
        };
        modEntry.OnUnload = entry => {
            MainCore.Dispose();
            entry.OnUpdate = null;
            entry.OnToggle = null;
            instance = null;
            return true;
        };
        return true;
    }
    public IQuartzLogger QuartzLogger => this;
    public string QuartzFilePath => dataPath;
    public string ModsPath => modPath;
    public string UserLibsPath => modPath;
    public bool SupportsSelfUpdate => true;
    public string UpdateAssetName => "QuartzUmm.zip";
    public string UpdateExtractRoot =>
        Directory.GetParent(modPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.FullName;
    public void QuartzMsg(string msg) => logger.Log(msg);
    public void QuartzWrn(string msg) => logger.Warning(msg);
    public void QuartzErr(string msg) => logger.Error(msg);
}
#endif

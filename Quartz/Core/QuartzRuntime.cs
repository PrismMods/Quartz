using GTweens.Contexts;
using Quartz.Async;
using Quartz.Compat;
using Quartz.Compat.Interface;
using Quartz.Core.Service;
using Quartz.Features.PlayCount;
using Quartz.Features.InGameOverlay;
using Quartz.Game.Stats;
using Quartz.IO;
using Quartz.Resource;
using Quartz.UI;
using Quartz.Update;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Quartz.Core;
public sealed class QuartzRuntime {
    public Version Version { get; }
    public Assembly Assembly { get; }
    public QuartzLogger Logger { get; }
    public ModState State { get; }
    public event Action<bool, bool> OnModEnabledChanged;
    public PathService Paths { get; }
    public SettingsFile<CoreSettings> Config { get; }
    public LocalizationService Localization { get; private set; }
    public ResourceManager Resource { get; }
    public SpriteManager Sprite { get; }
    public GameObject RootObject { get; private set; }
    public GTweensContext TweensContext { get; }
    public readonly IQuartzHost Host;
    private readonly RuntimeServices services;
    private readonly RuntimeTicks ticks;
    private readonly FeatureRegistry features = new();
    private UIService uiService;
    private TweenService tweenService;
    private HarmonyService harmonyService;
    private PlayCount playCount;
    private UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.LoadSceneMode> xperfectGuardHandler;
    public QuartzRuntime(IQuartzHost host) {
        Host = host;
        Version = new Version(Info.Version);
        Assembly = Assembly.GetExecutingAssembly();
        Logger = new QuartzLogger(host.QuartzLogger);
        Diag.Bind(Logger.Wrn);
        State = new ModState();
        Paths = new PathService(host.QuartzFilePath);
        Config = new SettingsFile<CoreSettings>(Paths.ConfigPath);
        Resource = new ResourceManager(Assembly, "Quartz.Resource.Embedded.");
        Sprite = new SpriteManager(Resource);
        services = new RuntimeServices();
        ticks = new RuntimeTicks();
        TweensContext = new GTweensContext();
    }
    private void MigrateLegacyData() {
        try {
            string newRoot = Host.QuartzFilePath;
            string parent = Path.GetDirectoryName(newRoot);
            if(string.IsNullOrEmpty(parent)) return;
            int moved = 0;
            foreach(string oldRoot in LegacyDataRoots(newRoot, parent)) {
                if(!Directory.Exists(oldRoot) ||
                   string.Equals(Path.GetFullPath(oldRoot), Path.GetFullPath(newRoot), StringComparison.OrdinalIgnoreCase)) continue;
                Directory.CreateDirectory(newRoot);
                foreach(string entry in Directory.GetFileSystemEntries(oldRoot)) {
                    string dest = Path.Combine(newRoot, Path.GetFileName(entry));
                    if(File.Exists(dest) || Directory.Exists(dest)) continue;
                    try {
                        if(Directory.Exists(entry)) Directory.Move(entry, dest);
                        else File.Move(entry, dest);
                        moved++;
                    } catch(Exception e) {
                        Logger.Wrn($"[Startup] migrate '{Path.GetFileName(entry)}' failed: {e.Message}");
                    }
                }
            }
            if(moved > 0) Logger.Msg($"[Startup] migrated {moved} item(s) from legacy Koren data into {newRoot}");
        } catch(Exception e) {
            Logger.Wrn($"[Startup] legacy data migration failed: {e.Message}");
        }
    }
    private IEnumerable<string> LegacyDataRoots(string newRoot, string parent) {
        yield return Path.Combine(parent, "Koren");
        string modRoot = Host.ModsPath;
        if(string.IsNullOrEmpty(modRoot) ||
           !string.Equals(NormalizeDir(parent), NormalizeDir(modRoot), StringComparison.OrdinalIgnoreCase)) yield break;
        string modParent = Path.GetDirectoryName(NormalizeDir(modRoot));
        if(!string.IsNullOrEmpty(modParent)) yield return Path.Combine(modParent, "Koren");
    }
    private static readonly string[] ModRootKeepSuffixes = { ".dll", ".dll.old", ".old", ".pdb", ".xml", ".krnew" };
    private static bool IsModDistributionEntry(string name) {
        if(name.Length == 0 || name[0] == '.') return true;
        if(string.Equals(name, "UserData", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(name, "Info.json", StringComparison.OrdinalIgnoreCase)) return true;
        foreach(string suffix in ModRootKeepSuffixes)
            if(name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    private static string NormalizeDir(string path) =>
        Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    private void MigrateModRootDataIntoDataRoot() {
        try {
            string dataRoot = Host.QuartzFilePath;
            string modRoot = Host.ModsPath;
            if(string.IsNullOrEmpty(dataRoot) || string.IsNullOrEmpty(modRoot) || !Directory.Exists(modRoot)) return;
            string parent = Path.GetDirectoryName(NormalizeDir(dataRoot));
            if(string.IsNullOrEmpty(parent) ||
               !string.Equals(parent, NormalizeDir(modRoot), StringComparison.OrdinalIgnoreCase)) return;
            string[] loose = Directory.GetFileSystemEntries(modRoot);
            int moved = 0;
            foreach(string entry in loose) {
                string name = Path.GetFileName(entry);
                if(IsModDistributionEntry(name)) continue;
                Directory.CreateDirectory(dataRoot);
                moved += MergeMove(entry, Path.Combine(dataRoot, name));
            }
            if(moved > 0) Logger.Msg($"[Startup] moved {moved} item(s) from the mod folder into UserData");
        } catch(Exception e) {
            Logger.Wrn($"[Startup] UserData layout migration failed: {e.Message}");
        }
    }
    private int MergeMove(string src, string dest) {
        try {
            if(Directory.Exists(src)) {
                if(!Directory.Exists(dest) && !File.Exists(dest)) {
                    Directory.Move(src, dest);
                    return 1;
                }
                int n = 0;
                foreach(string child in Directory.GetFileSystemEntries(src))
                    n += MergeMove(child, Path.Combine(dest, Path.GetFileName(child)));
                try {
                    if(Directory.GetFileSystemEntries(src).Length == 0) Directory.Delete(src);
                } catch(Exception e) { Diag.Ignore(e); }
                return n;
            }
            if(!File.Exists(src)) return 0;
            if(Directory.Exists(dest)) return 0;
            if(File.Exists(dest)) {
                // A previous partial migration or a user-created file can leave
                // both locations populated. Never guess which copy is newer:
                // keep the loose source so the conflict remains recoverable.
                Logger.Wrn($"[Startup] kept conflicting legacy file '{Path.GetFileName(src)}'; destination already exists");
                return 0;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Move(src, dest);
            return 1;
        } catch(Exception e) {
            Logger.Wrn($"[Startup] move '{Path.GetFileName(src)}' failed: {e.Message}");
            return 0;
        }
    }
    private void TryLegacyRenameUpgrade() {
        try {
            string dllPath = Assembly.Location;
            if(string.IsNullOrEmpty(dllPath) ||
               !string.Equals(Path.GetFileName(dllPath), "Koren.dll", StringComparison.OrdinalIgnoreCase)) {
                string probe = Path.Combine(Host.ModsPath, "Koren.dll");
                bool quartzPresent = File.Exists(Path.Combine(Host.ModsPath, "Quartz.dll"));
                if(string.IsNullOrEmpty(dllPath) && File.Exists(probe) && !quartzPresent) dllPath = probe;
                else return;
            }
            Logger.Msg("[Startup] running as Koren.dll — fetching Quartz release to migrate install");
            UpdateService.InstallLegacyRename(dllPath);
        } catch(Exception e) {
            Logger.Wrn($"[Startup] legacy rename upgrade failed: {e.Message}");
        }
    }
    public void Initialize() {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var total = System.Diagnostics.Stopwatch.StartNew();
        MigrateModRootDataIntoDataRoot();
        MigrateLegacyData();
        Paths.Initialize();
        foreach(string stale in new[] { "Quartz.dll.old", "QuartzUmm.dll.old", "Koren.dll.old" }) {
            try {
                string oldDll = Path.Combine(Host.ModsPath, stale);
                if(File.Exists(oldDll)) File.Delete(oldDll);
            } catch(Exception e) {
                Logger.Wrn($"[Startup] couldn't remove {stale}: {e.Message}");
            }
        }
        Quartz.Plugins.PluginIdentityResolver.Hook();
        CreateRootObject();
        RootObject.AddComponent<MainThread>();
        SaveGate.DeferWrites = static () => Game.Stats.GameStats.InGame && !UI.UICore.IsOpen;
        Config.Load();
        ProfileManager.Initialize();
        Logger.Msg($"[Startup] paths + config took {sw.ElapsedMilliseconds} ms");
        sw.Restart();
        FontManager.Initialize();
        FontManager.OnFontChanged += InGameOverlayFont.Refresh;
        InGameOverlayFont.Initialize();
        Logger.Msg($"[Startup] FontManager took {sw.ElapsedMilliseconds} ms");
        Localization = new LocalizationService(Paths.LangPath, Config, Logger);
        uiService = new UIService();
        tweenService = new TweenService(TweensContext);
        harmonyService = new HarmonyService();
        playCount = new PlayCount();
        services.Add(Localization);
        services.Add(Quartz.Modules.ModuleService.Service);
        services.Add(Quartz.Addons.AddonService.Service);
        services.Add(uiService);
        services.Add(tweenService);
        services.Add(playCount);
        services.Add(harmonyService);
        ticks.Add(playCount);
        ticks.Add(harmonyService);
        ticks.Add(uiService);
        ticks.Add(tweenService);
        ticks.Add(Quartz.Modules.ModuleService.Ticker);
        ticks.Add(Quartz.Addons.AddonService.Ticker);
        services.Initialize(Logger);
        Quartz.Features.Interop.XPerfectRecursionGuard.TryApply(harmonyService.Harmony);
        xperfectGuardHandler = (_, _) => Quartz.Features.Interop.XPerfectRecursionGuard.TryApply(harmonyService.Harmony);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += xperfectGuardHandler;
        sw.Restart();
        RegisterOverlayHooks();
        RegisterFeatures();
        SetModEnabled(Config.Data.Active, false);
        Logger.Msg($"[Startup] SetModEnabled took {sw.ElapsedMilliseconds} ms");
        if(Host.SupportsSelfUpdate) {
            TryLegacyRenameUpgrade();
            UpdateService.Check();
        }
        Logger.Msg($"[Startup] total {total.ElapsedMilliseconds} ms");
        if(Quartz.Features.Interop.UmmInterop.IsPresent) Logger.Msg($"[Umm] active mods: [{string.Join(", ", Quartz.Features.Interop.UmmInterop.ActiveModIds())}]");
        else Logger.Msg("[Umm] not present");
        Logger.Msg("Hello");
    }
    public void Tick() => ticks.Tick();
    public void Dispose() {
        static void Safe(Action step) {
            try {
                step();
            } catch(Exception e) { Diag.Ignore(e); }
        }
        Safe(() => SetModEnabled(false, true));
        Safe(() => FontManager.OnFontChanged -= InGameOverlayFont.Refresh);
        Safe(InGameOverlayFont.Unhook);
        Safe(() => {
            if(xperfectGuardHandler != null) {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= xperfectGuardHandler;
                xperfectGuardHandler = null;
            }
        });
        Safe(() => Config.Save());
        Safe(() => ProfileManager.CaptureActive());
        Safe(() => services.Dispose());
        Safe(() => Sprite.Dispose());
        Safe(() => Quartz.Resource.FontManager.Dispose());
        Safe(() => Resource.Dispose());
        if(RootObject != null) {
            Safe(() => Object.Destroy(RootObject));
            RootObject = null;
        }
        Safe(Quartz.Plugins.PluginIdentityResolver.Unhook);
        Safe(Quartz.Features.Interop.XPerfectBridge.Unhook);
        Safe(Quartz.Features.Interop.XPerfectRecursionGuard.Unhook);
        Safe(Diag.Unbind);
        Logger.Msg("Bye");
    }
    private static void RegisterOverlayHooks() {
    }
    private void RegisterFeatures() {
        features.Register("InGameOverlayFont", InGameOverlayFont.Refresh, InGameOverlayFont.RestoreAll);
    }
    public void SetModEnabled(bool enabled, bool isDispose) {
        if(State.IsEnabled == enabled) return;
        State.IsEnabled = enabled;
        if(!isDispose) {
            Config.Data.Active = enabled;
            Config.RequestSave();
        }
        if(enabled) {
            features.EnableAll();
            OnModEnabledChanged?.Invoke(true, isDispose);
            Logger.Msg("Mod Enabled");
        } else {
            OnModEnabledChanged?.Invoke(false, isDispose);
            features.DisableAll();
            Logger.Msg("Mod Disabled");
        }
    }
    private void CreateRootObject() {
        RootObject = new GameObject("Quartz");
        Object.DontDestroyOnLoad(RootObject);
    }
}

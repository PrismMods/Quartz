using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Modules;
public static partial class ModuleMigration {
    public const string BundledFolderName = "Module.bundled";
    public static string BundledPath => Path.Combine(MainCore.Paths.RootPath, BundledFolderName);
    public static void RunOnce(ModuleState state) {
        if(state == null || state.Migrated) return;
        state.Migrated = true;
        if(File.Exists(ModuleState.Path)) {
            state.Save();
            return;
        }
        Plan plan = Decide(ReadSettings);
        if(!plan.IsUpgrade) {
            state.Save();
            return;
        }
        List<string> installed = [];
        foreach(string id in plan.Install) {
            if(!CopyBundled(id)) continue;
            ModuleState.Entry entry = state.For(id);
            entry.Enabled = true;
            entry.Source = "migrated";
            installed.Add(id);
        }
        state.Save();
        MainCore.Log.Msg(installed.Count == 0
            ? "[Modules] first run on an existing install — no modules to carry over"
            : $"[Modules] carried {installed.Count} feature(s) over from your previous install: {string.Join(", ", installed)}");
    }
    private static JObject ReadSettings(string fileName) {
        string path = Path.Combine(MainCore.Paths.RootPath, fileName.Replace('/', Path.DirectorySeparatorChar));
        try {
            return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : null;
        } catch {
            return null;
        }
    }
    private static bool CopyBundled(string id) {
        string source = BundledPath;
        string binary = Path.Combine(source, id + ModuleService.ModuleExtension);
        string manifest = Path.Combine(source, id + ModuleService.ManifestExtension);
        if(!File.Exists(binary) || !File.Exists(manifest)) return false;
        try {
            string root = MainCore.Paths.ModulePath;
            Directory.CreateDirectory(root);
            File.Copy(binary, Path.Combine(root, id + ModuleService.ModuleExtension), true);
            File.Copy(manifest, Path.Combine(root, id + ModuleService.ManifestExtension), true);
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[Modules] couldn't carry over '{id}': {e.Message}");
            return false;
        }
    }
}

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
    public static void ApplySplits(ModuleState state) {
        if(state == null) return;
        bool changed = false;
        foreach(Split split in Splits) {
            if(!state.Modules.TryGetValue(split.From, out ModuleState.Entry from)) continue;
            RefreshSplitSource(split.From);
            if(state.Modules.ContainsKey(split.To)) continue;
            if(!ModuleBundle.Copy(split.To)) continue;
            ModuleState.Entry entry = state.For(split.To);
            entry.Enabled = from.Enabled;
            entry.Source = "split";
            changed = true;
            MainCore.Log.Msg($"[Modules] '{split.To}' split out of '{split.From}' — installed automatically");
        }
        if(changed) state.Save();
    }
    private static void RefreshSplitSource(string id) {
        string bundled = ModuleBundle.VersionOf(id);
        string installed = ModuleBundle.VersionAt(
            Path.Combine(MainCore.Paths.ModulePath, id + ModuleService.ManifestExtension));
        if(!NeedsSourceRefresh(installed, bundled)) return;
        if(!ModuleBundle.Copy(id)) return;
        MainCore.Log.Msg(
            $"[Modules] refreshed '{id}' from the bundled v{bundled} — the installed v{installed} predates a split "
            + "and still owns pages the split-out module registers");
    }
    private static JObject ReadSettings(string fileName) {
        string path = Path.Combine(MainCore.Paths.RootPath, fileName.Replace('/', Path.DirectorySeparatorChar));
        try {
            return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : null;
        } catch(Exception e) {
            Diag.Ignore(e);
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

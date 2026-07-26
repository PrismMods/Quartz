using Quartz.Core;
using Quartz.IO;
namespace Quartz.Modules;
public sealed partial class ModuleState {
    public static string Path => System.IO.Path.Combine(MainCore.Paths.ModulePath, "installed.json");
    public static ModuleState Load() {
        try {
            return File.Exists(Path) ? Parse(File.ReadAllText(Path)) : new ModuleState();
        } catch(Exception e) {
            MainCore.Log.Wrn($"[Modules] couldn't read installed.json: {e.Message}");
            return new ModuleState();
        }
    }
    public void Save() {
        try {
            Directory.CreateDirectory(MainCore.Paths.ModulePath);
            AtomicFile.WriteAllText(Path, ToJson());
        } catch(Exception e) {
            MainCore.Log.Err($"[Modules] couldn't write installed.json: {e.Message}");
        }
    }
}

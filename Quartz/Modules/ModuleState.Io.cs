using Quartz.Core;
using Quartz.IO;
namespace Quartz.Modules;
public sealed partial class ModuleState {
    public static string Path => System.IO.Path.Combine(MainCore.Paths.ModulePath, "installed.json");
    public static ModuleState Load() {
        try {
            if(!File.Exists(Path)) return new ModuleState();
            ModuleState state = Parse(File.ReadAllText(Path));
            if(state.Corrupt) {
                // keep the user's list before the next Save overwrites it; skip the 1.x migration too
                File.Copy(Path, Path + ".corrupt", true);
                state.Migrated = true;
                MainCore.Log.Wrn("[Modules] installed.json is corrupt; saved a copy as installed.json.corrupt");
            }
            return state;
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

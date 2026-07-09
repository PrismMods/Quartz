using Newtonsoft.Json;
using Quartz.Core;
using Quartz.IO;
namespace Quartz.Addons;
public sealed class AddonSettings<T> : ISettingsHandle where T : class, new() {
    public T Data { get; } = new();
    public string Path { get; }
    public AddonSettings(string path) {
        Path = path;
        SettingsRegistry.Register(this);
    }
    public bool Load() {
        try {
            if(!File.Exists(Path)) return false;
            JsonConvert.PopulateObject(File.ReadAllText(Path), Data);
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[Addons] failed to load settings '{Path}': {e}");
            return false;
        }
    }
    public void LoadOrDefaults() {
        try {
            JsonConvert.PopulateObject(JsonConvert.SerializeObject(new T()), Data);
        } catch(Exception e) {
            MainCore.Log.Err($"[Addons] failed to reset settings '{Path}': {e}");
        }
        Load();
    }
    public bool Save() {
        try {
            string dir = System.IO.Path.GetDirectoryName(Path);
            if(!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            AtomicFile.WriteAllText(Path, JsonConvert.SerializeObject(Data, Formatting.Indented));
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[Addons] failed to save settings '{Path}': {e}");
            return false;
        }
    }
    public void CancelPendingSave() { }
}

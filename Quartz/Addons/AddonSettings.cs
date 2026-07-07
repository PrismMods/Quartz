using Newtonsoft.Json;
using Quartz.Core;
using Quartz.IO;

namespace Quartz.Addons;

// Per-addon settings persistence. Unlike feature settings (hand-written
// JObject mapping), addon settings are plain POCOs round-tripped through
// Newtonsoft object mapping — good enough for addon-sized config and zero
// boilerplate on the addon side. Missing fields keep their defaults
// (PopulateObject), unknown fields are ignored.
//
// Registered as an ISettingsHandle so the profile system snapshots and
// reloads addon settings exactly like feature settings. Lives in the data
// root as Addon.<id>.json (flat, next to every other settings file) so
// profile capture picks it up.
//
// Keep the settings type to plain fields (bool/int/float/string/lists).
// Unity structs like Color have self-referential properties that Newtonsoft
// object mapping chokes on — store floats and build the Color yourself.
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

    // Reset to a fresh T's values, then overlay the file if present — same
    // contract SettingsFile<T>.LoadOrDefaults has for profile switching.
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

    // Addon settings save rarely; no debounce needed.
    public void CancelPendingSave() { }
}

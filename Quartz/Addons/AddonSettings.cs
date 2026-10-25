using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.IO;
using UnityEngine;
namespace Quartz.Addons;
public sealed class AddonSettings<T> : ISettingsHandle where T : class, new() {
    private static readonly JsonSerializerSettings SerializerSettings = new() {
        Converters = { new UnityColorJsonConverter() },
    };
    public T Data { get; } = new();
    public string Path { get; }
    public AddonSettings(string path) {
        Path = path;
        SettingsRegistry.Register(this);
    }
    public bool Load() {
        try {
            if(!File.Exists(Path)) return false;
            Populate(File.ReadAllText(Path));
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[Addons] failed to load settings '{Path}': {e}");
            return false;
        }
    }
    public void LoadOrDefaults() {
        try {
            Populate(JsonConvert.SerializeObject(new T(), SerializerSettings));
        } catch(Exception e) {
            MainCore.Log.Err($"[Addons] failed to reset settings '{Path}': {e}");
        }
        Load();
    }
    // PopulateObject appends into lists and dictionaries; clear the ones the JSON sets.
    // shortcut: top-level members only, nested objects still merge; recurse if addon settings grow nested collections.
    private void Populate(string json) {
        JObject incoming = JObject.Parse(json);
        foreach(MemberInfo member in typeof(T).GetMembers(BindingFlags.Public | BindingFlags.Instance)) {
            if(!incoming.TryGetValue(member.Name, StringComparison.OrdinalIgnoreCase, out _)) continue;
            object value = member switch {
                FieldInfo field => field.GetValue(Data),
                PropertyInfo { CanRead: true } property when property.GetIndexParameters().Length == 0
                    => property.GetValue(Data),
                _ => null,
            };
            if(value is IList { IsFixedSize: false } list) list.Clear();
            else if(value is IDictionary dictionary) dictionary.Clear();
        }
        JsonConvert.PopulateObject(json, Data, SerializerSettings);
    }
    public bool Save() {
        try {
            string dir = System.IO.Path.GetDirectoryName(Path);
            if(!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            AtomicFile.WriteAllText(Path, JsonConvert.SerializeObject(Data, Formatting.Indented, SerializerSettings));
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[Addons] failed to save settings '{Path}': {e}");
            return false;
        }
    }
    public void CancelPendingSave() { }
}

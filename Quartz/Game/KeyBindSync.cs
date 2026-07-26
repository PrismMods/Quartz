using Quartz.Core;
namespace Quartz.Game;
public static class KeyBindSync {
    private static string ownerId;
    private static Func<bool> isSyncing;
    private static Action<bool> setSyncing;
    private static bool defaultValue;
    public static event Action Changed;
    public static bool Available => isSyncing != null;
    public static bool Default => defaultValue;
    public static void Register(string id, bool defaultOn, Func<bool> get, Action<bool> set) {
        if(string.IsNullOrWhiteSpace(id) || get == null || set == null)
            throw new ArgumentException("a key-bind sync source needs an id and both delegates");
        ownerId = id;
        defaultValue = defaultOn;
        isSyncing = get;
        setSyncing = set;
        Raise();
    }
    public static void Unregister(string id) {
        if(id != ownerId) return;
        ownerId = null;
        isSyncing = null;
        setSyncing = null;
        Raise();
    }
    public static bool IsSyncing {
        get {
            try {
                return isSyncing != null && isSyncing();
            } catch(Exception e) {
                MainCore.Log.Err($"[Input] key-bind sync source '{ownerId}' threw: {e.Message}");
                return false;
            }
        }
    }
    public static void SetSyncing(bool value) {
        try {
            setSyncing?.Invoke(value);
        } catch(Exception e) {
            MainCore.Log.Err($"[Input] key-bind sync source '{ownerId}' threw: {e.Message}");
        }
    }
    public static void Raise() {
        try {
            Changed?.Invoke();
        } catch(Exception e) {
            MainCore.Log.Err($"[Input] key-bind sync listener threw: {e.Message}");
        }
    }
}

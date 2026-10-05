using Quartz.Core;
namespace Quartz.Game;
public static class VoiceDeafen {
    private static string ownerId;
    private static Func<string> state;
    private static Action<bool> setDeaf;
    public static bool Registered => setDeaf != null;
    public static void Register(string id, Func<string> status, Action<bool> set) {
        if(string.IsNullOrWhiteSpace(id) || status == null || set == null)
            throw new ArgumentException("a voice deafen source needs an id and both delegates");
        ownerId = id;
        state = status;
        setDeaf = set;
    }
    public static void Unregister(string id) {
        if(id != ownerId) return;
        ownerId = null;
        state = null;
        setDeaf = null;
    }
    public static string Status {
        get {
            try {
                return state?.Invoke() ?? "not installed";
            } catch(Exception e) {
                MainCore.Log.Err($"[Voice] deafen source '{ownerId}' threw: {e.Message}");
                return "error";
            }
        }
    }
    public static void SetDeaf(bool deaf) {
        try {
            setDeaf?.Invoke(deaf);
        } catch(Exception e) {
            MainCore.Log.Err($"[Voice] deafen source '{ownerId}' threw: {e.Message}");
        }
    }
}

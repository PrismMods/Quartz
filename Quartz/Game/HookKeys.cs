using Quartz.Core;
using UnityEngine;
namespace Quartz.Game;
public static class HookKeys {
    private sealed class Source {
        public string Id;
        public Func<KeyCode, bool> Held;
        public Func<KeyCode, bool> Tracked;
    }
    private static readonly List<Source> sources = [];
    public static void Register(string id, Func<KeyCode, bool> held, Func<KeyCode, bool> tracked) {
        if(string.IsNullOrWhiteSpace(id) || held == null || tracked == null)
            throw new ArgumentException("a hook-key source needs an id and both delegates");
        Unregister(id);
        sources.Add(new Source { Id = id, Held = held, Tracked = tracked });
    }
    public static void Unregister(string id) {
        if(!string.IsNullOrEmpty(id)) sources.RemoveAll(s => s.Id == id);
    }
    public static event Action<KeyCode, bool> KeyEvent;
    public static void RaiseKeyEvent(KeyCode key, bool down) {
        Action<KeyCode, bool> handlers = KeyEvent;
        if(handlers == null) return;
        try {
            handlers(key, down);
        } catch(Exception e) { Diag.Ignore(e); }
    }
    public static bool Held(KeyCode key) => Ask(key, static s => s.Held);
    public static bool Tracked(KeyCode key) => Ask(key, static s => s.Tracked);
    private static bool Ask(KeyCode key, Func<Source, Func<KeyCode, bool>> pick) {
        for(int i = 0; i < sources.Count; i++) {
            try {
                if(pick(sources[i])(key)) return true;
            } catch(Exception e) {
                MainCore.Log.Err($"[Input] hook-key source '{sources[i].Id}' threw: {e.Message}");
            }
        }
        return false;
    }
}

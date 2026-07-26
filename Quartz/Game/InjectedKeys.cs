using Quartz.Core;
using UnityEngine;
namespace Quartz.Game;
public static class InjectedKeys {
    private sealed class Source {
        public string Id;
        public Func<KeyCode, bool> IsInjected;
        public Func<bool> GuardActive;
    }
    private static readonly List<Source> sources = [];
    public static void Register(string id, Func<KeyCode, bool> isInjected, Func<bool> guardActive) {
        if(string.IsNullOrWhiteSpace(id) || isInjected == null || guardActive == null)
            throw new ArgumentException("an injected-key source needs an id and both delegates");
        Unregister(id);
        sources.Add(new Source { Id = id, IsInjected = isInjected, GuardActive = guardActive });
    }
    public static void Unregister(string id) {
        if(!string.IsNullOrEmpty(id)) sources.RemoveAll(s => s.Id == id);
    }
    public static bool Is(KeyCode key) {
        for(int i = 0; i < sources.Count; i++) {
            try {
                if(sources[i].IsInjected(key)) return true;
            } catch(Exception e) {
                MainCore.Log.Err($"[Input] injected-key source '{sources[i].Id}' threw: {e.Message}");
            }
        }
        return false;
    }
    public static bool GuardActive {
        get {
            for(int i = 0; i < sources.Count; i++) {
                try {
                    if(sources[i].GuardActive()) return true;
                } catch(Exception e) {
                    MainCore.Log.Err($"[Input] injected-key source '{sources[i].Id}' threw: {e.Message}");
                }
            }
            return false;
        }
    }
}

using System.Collections.Concurrent;
namespace Quartz.Features.Discord;
public static class UserCache {
    private static readonly ConcurrentDictionary<string, string> names = new();
    public static void Remember(string id, string name) {
        if(string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name) || name == "?") return;
        names[id] = name;
    }
    public static string Resolve(string id) => names.TryGetValue(id, out string name) ? name : null;
    public static void Clear() => names.Clear();
}

namespace Quartz.Overlay;
public sealed class StatSource {
    public string Id;
    public string Label;
    public string Category;
    public Func<string> Value;
}
public static class StatRegistry {
    private static readonly List<StatSource> sources = [];
    private static readonly HashSet<string> ids = new(StringComparer.Ordinal);
    public static IReadOnlyList<StatSource> Sources => sources;
    public static int Version { get; private set; }
    public static bool IsRegistered(string statId) => statId != null && ids.Contains(statId);
    public static void Register(StatSource stat) {
        if(stat == null || string.IsNullOrWhiteSpace(stat.Id) || stat.Value == null)
            throw new ArgumentException("stat needs an Id and a Value delegate");
        if(!ids.Add(stat.Id))
            throw new InvalidOperationException($"stat id '{stat.Id}' is already registered");
        sources.Add(stat);
        Version++;
    }
    public static void Unregister(string statId) {
        if(string.IsNullOrEmpty(statId) || !ids.Remove(statId)) return;
        sources.RemoveAll(s => s.Id == statId);
        Version++;
    }
}

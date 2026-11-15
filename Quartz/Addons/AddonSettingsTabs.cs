namespace Quartz.Addons;
/// <summary>
/// The addon pages registered as settings tabs (RegisterSettingsTab, RegisterTopLevelSettingsTab), by page key, so the new menu
/// can draw them from their settings object. A custom RegisterTab page has no record and keeps the classic card.
/// </summary>
internal static class AddonSettingsTabs {
    // A class, not a record: netstandard2.1 has no init accessors. An undo's document is the Tab itself, compared by reference.
    internal sealed class Tab(string key, string addonId, object data, object defaults, Action save, Action<string> warn) {
        internal string Key { get; } = key;
        internal string AddonId { get; } = addonId;
        internal object Data { get; } = data;
        internal object Defaults { get; } = defaults;
        internal Action Save { get; } = save;
        internal Action<string> Warn { get; } = warn;
    }

    private static readonly Dictionary<string, Tab> tabs = [];

    internal static IReadOnlyCollection<string> Keys => tabs.Keys;

    internal static void Register(Tab tab) => tabs[tab.Key] = tab;

    internal static void Remove(string key) => tabs.Remove(key);

    /// <summary>Every tab the addon registered, as its unload removes its pages.</summary>
    internal static void RemoveAddon(string addonId) {
        foreach(string key in tabs.Where(kv => kv.Value.AddonId == addonId).Select(kv => kv.Key).ToList()) tabs.Remove(key);
    }

    internal static bool TryGet(string key, out Tab tab) => tabs.TryGetValue(key, out tab);
}

namespace Quartz.UI.FacetUi;
/// <summary>
/// The new menu shell's pure rules: rail grouping, quick-switch lists, last-page and tab lookup, and the first window
/// rectangle. BCL only, so the test project can link it and pin the behaviour without the game.
/// </summary>
internal static class ShellModel {
    /// <summary>The mockup's six quick switches, in order.</summary>
    internal static readonly string[] DefaultSwitches = ["keyviewer", "combo", "keylimiter", "effectremover", "autodeafen", "progressbar"];

    /// <summary>Rail group for a category key: 0 leads, 2 closes, 1 sits between, -1 stays off the rail.</summary>
    internal static int RailGroup(string categoryKey) => categoryKey switch {
        "home" or "features" => 0,
        "profiles" or "import" or "settings" or "developer" => 2,
        "credits" or null => -1,
        _ => 1,
    };

    /// <summary>Stored switch ids, filtered to the known ones. Null gives the defaults that exist; "" gives none.</summary>
    internal static List<string> ParseSwitches(string stored, ICollection<string> known) {
        var result = new List<string>();
        if(stored == null) {
            foreach(string id in DefaultSwitches) if(known.Contains(id)) result.Add(id);
            return result;
        }
        foreach(string raw in stored.Split(',')) {
            string id = raw.Trim();
            if(known.Contains(id) && !result.Contains(id)) result.Add(id);
        }
        return result;
    }

    internal static string FormatSwitches(IEnumerable<string> ids) => string.Join(",", ids);

    /// <summary>The stored page when it is set and still exists, else Home.</summary>
    internal static string LastPage(string stored, Func<string, bool> exists, string home) =>
        !string.IsNullOrEmpty(stored) && exists(stored) ? stored : home;

    /// <summary>
    /// The tab for the open page: first by page key, then by owning feature (so a feature's stub finds the feature's
    /// real tab), else -1 to keep Facet's remembered tab.
    /// </summary>
    internal static int TabIndex(IReadOnlyList<KeyValuePair<string, string>> pages, string currentKey, string currentFeature) {
        for(int i = 0; i < pages.Count; i++) if(pages[i].Key == currentKey) return i;
        if(currentFeature != null) for(int i = 0; i < pages.Count; i++) if(pages[i].Value == currentFeature) return i;
        return -1;
    }

    /// <summary>The first window rectangle: 80% of the panel, centred.</summary>
    internal static (float X, float Y, float W, float H) InitialRect(float panelW, float panelH) =>
        (panelW * 0.1f, panelH * 0.1f, panelW * 0.8f, panelH * 0.8f);

    /// <summary>
    /// Features that offer the shared layout editor. Mirrors the features that call SetLayoutTarget (see
    /// Quartz/UI/Utility/LayoutIds.cs), so a new target must be added here too.
    /// </summary>
    internal static bool HasLayout(string featureId) => featureId is "keyviewer" or "combo" or "judgement" or "songtitle"
        or "progressbar" or "practice" or "calibration" or "panels";
}

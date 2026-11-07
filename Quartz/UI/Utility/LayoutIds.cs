namespace Quartz.UI.Utility;
/// <summary>
/// The ids PrismLib's shared layout editor knows Quartz's movable elements by (ReorganizeHandle.SetLayoutTarget). Stable:
/// another mod's editor keys its undo steps by them. A panel's id is only its place in the list; its target finds it by
/// its PanelConfig. BCL only, so the test project pins the order.
/// </summary>
internal static class LayoutIds {
    internal const string KeyViewer = "keyviewer";
    internal const string KeyViewerFoot = "keyviewer.foot";
    internal const string Tabub = "keyviewer.tabub";
    internal const string Combo = "combo";
    internal const string Judgement = "judgement";
    internal const string SongTitle = "songtitle";
    internal const string ProgressBar = "progressbar";
    internal const string Practice = "practice";
    internal const string Calibration = "calibration";
    private const string PanelPrefix = "panel.";
    internal static string Panel(int index) => PanelPrefix + index;
    private static readonly string[] Fixed = [KeyViewer, KeyViewerFoot, Tabub, Combo, Judgement, SongTitle, ProgressBar, Practice, Calibration];
    /// <summary>Sort key: the fixed ids in the order above, then panels by number; anything else last.</summary>
    internal static int Order(string id) {
        int i = Array.IndexOf(Fixed, id);
        if(i >= 0) return i;
        return id != null && id.StartsWith(PanelPrefix, StringComparison.Ordinal)
            && int.TryParse(id.AsSpan(PanelPrefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n)
            ? Fixed.Length + n : int.MaxValue;
    }
}

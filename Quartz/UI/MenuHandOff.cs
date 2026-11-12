namespace Quartz.UI;
/// <summary>
/// Which menu a user's close of the menu now showing goes back to. Opening one menu from the other sets it, and a
/// new value replaces the old, so at most one hand-back is ever pending.
/// </summary>
internal static class MenuHandOff {
    internal enum Menu { None, Classic, New }
    internal static Menu ReturnTo;
    /// <summary>
    /// The new menu came from the hub's Settings: after a trip away (a classic page, Edit layout) and back, its close still
    /// goes to the hub, and the hotkey over any of those screens shuts everything. Cleared once nothing shows.
    /// </summary>
    internal static bool FromHub;
}

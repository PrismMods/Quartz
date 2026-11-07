namespace Quartz.UI;
/// <summary>
/// Which menu a user's close of the menu now showing goes back to. Opening one menu from the other sets it, and a
/// new value replaces the old, so at most one hand-back is ever pending.
/// </summary>
internal static class MenuHandOff {
    internal enum Menu { None, Classic, New }
    internal static Menu ReturnTo;
}

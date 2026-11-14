namespace Quartz.UI.FacetUi;
/// <summary>
/// The form pages' pure rules: which judgement restriction modes and auto-deafen modes a page offers, and how a written
/// hotkey chord splits. BCL only, so the test project links it.
/// </summary>
internal static class PageRules {
    /// <summary>Mode 2 (X-Perfect only) needs XPerfect.</summary>
    internal static int[] RestrictModes(bool xperfect) => xperfect ? [0, 1, 2, 3, 4] : [0, 1, 3, 4];

    /// <summary>A stored 2 without XPerfect shows as 1, as the classic page shows it; the stored value is left alone.</summary>
    internal static int ShownMode(int stored, bool xperfect) => stored == 2 && !xperfect ? 1 : stored;

    /// <summary>AutoDeafenSettings' mode strings (repeated here: this file is BCL): shortcut where a shortcut can be sent,
    /// the bot always, Discord where voice control works.</summary>
    internal static string[] DeafenModes(bool shortcut, bool voice) {
        var modes = new List<string>(3);
        if(shortcut) modes.Add("shortcut");
        modes.Add("bot");
        if(voice) modes.Add("discord");
        return [.. modes];
    }

    /// <summary>"Ctrl + F8" gives ("Ctrl", "F8"), "F8" gives ("", "F8"); the first " + " splits, so "Keypad +" stays whole.</summary>
    internal static bool SplitChord(string text, out string modifier, out string key) {
        modifier = key = "";
        if(string.IsNullOrEmpty(text)) return false;
        int at = text.IndexOf(" + ", StringComparison.Ordinal);
        if(at < 0) key = text;
        else (modifier, key) = (text[..at], text[(at + 3)..]);
        return true;
    }
}

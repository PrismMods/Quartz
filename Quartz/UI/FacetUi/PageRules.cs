namespace Quartz.UI.FacetUi;
/// <summary>
/// The pages' pure rules: which judgement restriction modes and auto-deafen modes a page offers, how a written hotkey
/// chord splits, and which pages stay classic. BCL only, so the test project links it.
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

    /// <summary>Why a page stays in the classic menu, or null when the new menu draws it (spec "Calls made after review" ›
    /// "Pages and order"). An addon page that is not a settings tab is custom UI only its addon can draw; a feature's page
    /// is owned too, but is never an addon page.</summary>
    internal static string StaysClassic(string key, bool addonPage, bool settingsTab) {
        if(key == "credits.main") return "off the new menu's rail; its content is the Settings page's Credits card";
        if(addonPage && !settingsTab)
            return "a custom addon page keeps a card linking to the classic menu ('Calls made after review' › 'Pages and order')";
        return null;
    }
}

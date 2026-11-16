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

    /// <summary>The judgement bar's bits, early to late. With X, Perfect (3) gives way to early ±, X and late ± (JudgementPopupHider's
    /// PlusPerfectBit 13, XPerfectPerfectBit 12, MinusPerfectBit 14; repeated here: this file is BCL). Early is − when minusEarly
    /// (the game's own X, whose PerfectMinus comes first), else + (the XPerfect mod's).</summary>
    internal static int[] BarBits(bool x, bool minusEarly) {
        if(!x) return [0, 1, 2, 3, 4, 5, 6];
        int early = minusEarly ? 14 : 13, late = minusEarly ? 13 : 14;
        return [0, 1, 2, early, 12, late, 4, 5, 6];
    }

    /// <summary>Whether a judgement restriction mode fails a judgement bit. 1 (Pure Perfect): all but Perfect (3); 2 (X-Perfect
    /// only, the bar's bits): all but Perfect and X (12); 3 (custom): the bits outside allowedMask, none when it is 0; 4 (No Too
    /// Early): Too Early (0) only; 0 (accuracy): none. Mode 1 leaves 12 blocked: as a hit it is HitKind.FailedFloor.</summary>
    internal static bool Blocked(int mode, int allowedMask, int bit) => mode switch {
        1 => bit != 3,
        2 => bit != 3 && bit != 12,
        3 => allowedMask != 0 && (allowedMask & 1 << bit) == 0,
        4 => bit == 0,
        _ => false,
    };

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

using Quartz.UI;
using UnityEngine;

namespace Quartz.Addons;

// Registry of addon-contributed settings pages. Addon pages live outside the
// OriginalMenuState enum: each registration is assigned the next dynamic page
// state (starting right past the enum), and the UI factories fold them in —
// PageFactory builds a page base + scrollable content per entry, MenuFactory
// lists them as children of the Addons category. States are allocated from a
// monotonic counter so a reload can never hand a live page's state to a
// different page mid-session.
public static class AddonUI {
    public sealed class PageDef {
        public string AddonId;
        public string Title;
        // Locale key derived from the title; missing keys fall back to the
        // title itself, so addons work without touching Lang files.
        public string LocaleKey;
        public Action<Transform> Build;
        public int State;
    }

    public static readonly int BaseState = Enum.GetValues(typeof(OriginalMenuState)).Length;

    private static readonly List<PageDef> pages = [];
    private static int nextState = -1;

    public static IReadOnlyList<PageDef> Pages => pages;

    public static bool IsAddonState(int state) => state >= BaseState;

    internal static PageDef Register(string addonId, string title, string localeKey, Action<Transform> build) {
        if(nextState < BaseState) nextState = BaseState;

        PageDef def = new() {
            AddonId = addonId,
            Title = title,
            LocaleKey = localeKey,
            Build = build,
            State = nextState++,
        };
        pages.Add(def);
        return def;
    }

    internal static void UnregisterAddon(string addonId) =>
        pages.RemoveAll(p => p.AddonId == addonId);

    internal static void Clear() => pages.Clear();
}

using Quartz.UI;
using UnityEngine;
namespace Quartz.Addons;
public static class AddonUI {
    public sealed class PageDef {
        public string AddonId;
        public string Title;
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

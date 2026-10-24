using Quartz.Async;
using Quartz.UI;
using Quartz.UI.Nav;
namespace Quartz.Features;
public static class FeatureCategories {
    public static bool IsEnabled(string categoryKey) =>
        !string.IsNullOrEmpty(categoryKey)
        && (!FeatureService.State.Categories.TryGetValue(categoryKey, out bool enabled) || enabled);
    public static void SetEnabled(string categoryKey, bool enabled) {
        if(string.IsNullOrEmpty(categoryKey) || IsEnabled(categoryKey) == enabled) return;
        FeatureService.State.Categories[categoryKey] = enabled;
        FeatureService.State.Save();
        MainThread.Enqueue(static () => {
            if(UICore.Pages.Count == 0) return;
            if(NavRegistry.ByState(UICore.CurrentMenuState) == null)
                UICore.CurrentMenuState = NavRegistry.FirstVisibleState();
            UICore.Rebuild();
        });
    }
    public static IReadOnlyList<NavCategory> Togglable() {
        List<NavCategory> found = [];
        foreach(NavCategory category in NavRegistry.AllCategories)
            if(category.Togglable) found.Add(category);
        return found;
    }
    public static List<FeatureService.Handle> In(string categoryKey) {
        List<FeatureService.Handle> found = [];
        foreach(FeatureService.Handle handle in FeatureService.Features)
            if(handle.Info.Group == categoryKey) found.Add(handle);
        found.Sort(static (a, b) => {
            int byOrder = a.Info.Order.CompareTo(b.Info.Order);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Id, b.Id);
        });
        return found;
    }
}

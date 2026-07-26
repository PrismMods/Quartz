using Quartz.Async;
using Quartz.UI;
using Quartz.UI.Nav;
namespace Quartz.Modules;
public static class ModuleCategories {
    public static bool IsEnabled(string categoryKey) =>
        !string.IsNullOrEmpty(categoryKey)
        && (!ModuleService.State.Categories.TryGetValue(categoryKey, out bool enabled) || enabled);
    public static void SetEnabled(string categoryKey, bool enabled) {
        if(string.IsNullOrEmpty(categoryKey) || IsEnabled(categoryKey) == enabled) return;
        ModuleService.State.Categories[categoryKey] = enabled;
        ModuleService.State.Save();
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
    public static IReadOnlyList<ModuleService.Handle> Installed(string categoryKey) {
        List<ModuleService.Handle> found = [];
        if(string.IsNullOrEmpty(categoryKey)) return found;
        foreach(ModuleService.Handle handle in ModuleService.Modules)
            if(Group(handle) == categoryKey) found.Add(handle);
        found.Sort(static (a, b) => {
            int byOrder = (a.Manifest?.Order ?? 0).CompareTo(b.Manifest?.Order ?? 0);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Id, b.Id);
        });
        return found;
    }
    public static IReadOnlyList<ModuleCatalogEntry> Available(string categoryKey) {
        List<ModuleCatalogEntry> found = [];
        ModuleCatalog catalog = ModuleCatalogService.Catalog;
        if(catalog == null || string.IsNullOrEmpty(categoryKey)) return found;
        foreach(ModuleCatalogEntry entry in catalog.Modules) {
            if(entry.Group != categoryKey) continue;
            if(ModuleService.Find(entry.Id) != null) continue;
            found.Add(entry);
        }
        return found;
    }
    public static IReadOnlyList<ModuleManifest> Bundled(string categoryKey) {
        List<ModuleManifest> found = [];
        if(string.IsNullOrEmpty(categoryKey)) return found;
        foreach(ModuleManifest manifest in ModuleBundle.Available())
            if(manifest.Group == categoryKey) found.Add(manifest);
        return found;
    }
    public static string Group(ModuleService.Handle handle) => handle?.Manifest?.Group ?? "";
}

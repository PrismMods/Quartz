using Quartz.Core;
namespace Quartz.UI.Nav;
public static class NavRegistry {
    private static readonly List<NavCategory> categories = [];
    private static readonly List<NavPage> pages = [];
    private static int nextState;
    public static int Version { get; private set; }
    public static void AddCategory(NavCategory category) {
        if(category == null || string.IsNullOrWhiteSpace(category.Key))
            throw new ArgumentException("a nav category needs a Key");
        if(categories.Any(c => c.Key == category.Key))
            throw new InvalidOperationException($"nav category '{category.Key}' is already registered");
        categories.Add(category);
        Version++;
    }
    public static NavPage AddPage(NavPage page) {
        if(page == null || string.IsNullOrWhiteSpace(page.Key))
            throw new ArgumentException("a nav page needs a Key");
        if(page.Build == null)
            throw new ArgumentException($"nav page '{page.Key}' needs a Build delegate");
        if(pages.Any(p => p.Key == page.Key))
            throw new InvalidOperationException($"nav page '{page.Key}' is already registered");
        page.State = nextState++;
        pages.Add(page);
        Version++;
        return page;
    }
    public static void RemoveOwner(string ownerId) {
        if(string.IsNullOrEmpty(ownerId)) return;
        int removed = pages.RemoveAll(p => p.OwnerId == ownerId);
        removed += categories.RemoveAll(c => c.OwnerId == ownerId);
        if(removed > 0) Version++;
    }
    public static void Clear() {
        categories.Clear();
        pages.Clear();
        nextState = 0;
        Version++;
    }
    public static IReadOnlyList<NavCategory> Categories {
        get {
            List<NavCategory> visible = [];
            foreach(NavCategory category in categories)
                if(category.IsVisible && HasVisiblePage(category.Key)) visible.Add(category);
            visible.Sort(CompareCategories);
            return visible;
        }
    }
    public static IReadOnlyList<NavCategory> AllCategories {
        get {
            List<NavCategory> all = [.. categories];
            all.Sort(CompareCategories);
            return all;
        }
    }
    public static IReadOnlyList<NavPage> PagesIn(string categoryKey) {
        List<NavPage> found = [];
        if(categoryKey == null) return found;
        foreach(NavPage page in pages)
            if(page.CategoryKey == categoryKey && page.IsVisible) found.Add(page);
        found.Sort(ComparePages);
        return found;
    }
    public static IReadOnlyList<NavPage> AllVisible() {
        List<NavPage> found = [];
        foreach(NavCategory category in Categories) found.AddRange(PagesIn(category.Key));
        return found;
    }
    public static NavCategory Category(string key) {
        if(key == null) return null;
        foreach(NavCategory category in categories)
            if(category.Key == key) return category;
        return null;
    }
    public static NavPage ByState(int state) {
        foreach(NavPage page in pages)
            if(page.State == state) return page;
        return null;
    }
    public static NavPage ByKey(string key) {
        if(key == null) return null;
        foreach(NavPage page in pages)
            if(page.Key == key) return page;
        return null;
    }
    public static int StateFor(string key) => ByKey(key)?.State ?? -1;
    public static string KeyFor(int state) => ByState(state)?.Key;
    public static string CategoryKeyFor(int state) => ByState(state)?.CategoryKey;
    public static bool ShowsSubmenu(NavCategory category) =>
        category != null && (category.AlwaysSubmenu || PagesIn(category.Key).Count > 1);
    public static int FirstVisibleState() {
        foreach(NavCategory category in Categories) {
            IReadOnlyList<NavPage> inCategory = PagesIn(category.Key);
            if(inCategory.Count > 0) return inCategory[0].State;
        }
        return -1;
    }
    public static string Label(NavPage page) =>
        page == null ? "?" : MainCore.Tr.Get(page.LocaleKey, page.Title);
    private static bool HasVisiblePage(string categoryKey) {
        foreach(NavPage page in pages)
            if(page.CategoryKey == categoryKey && page.IsVisible) return true;
        return false;
    }
    private static int CompareCategories(NavCategory a, NavCategory b) {
        int byOrder = a.Order.CompareTo(b.Order);
        return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Key, b.Key);
    }
    private static int ComparePages(NavPage a, NavPage b) {
        int byOrder = a.Order.CompareTo(b.Order);
        return byOrder != 0 ? byOrder : a.State.CompareTo(b.State);
    }
}

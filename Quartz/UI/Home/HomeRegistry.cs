using Quartz.Core;
namespace Quartz.UI.Home;
public static class HomeRegistry {
    private static readonly List<HomeCard> cards = [];
    public static HomeCard Add(HomeCard card) {
        if(card == null || string.IsNullOrWhiteSpace(card.Key))
            throw new ArgumentException("a home card needs a Key");
        if(card.Build == null)
            throw new ArgumentException($"home card '{card.Key}' needs a Build delegate");
        cards.RemoveAll(c => c.Key == card.Key);
        cards.Add(card);
        return card;
    }
    public static void RemoveOwner(string ownerId) {
        if(!string.IsNullOrEmpty(ownerId)) cards.RemoveAll(c => c.OwnerId == ownerId);
    }
    public static IReadOnlyList<HomeCard> Visible() {
        List<HomeCard> found = [];
        foreach(HomeCard card in cards) {
            bool visible;
            try {
                visible = card.IsVisible;
            } catch(Exception e) {
                MainCore.Log.Err($"[Home] card '{card.Key}' visibility threw: {e.Message}");
                continue;
            }
            if(visible) found.Add(card);
        }
        found.Sort(static (a, b) => {
            int byOrder = a.Order.CompareTo(b.Order);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Key, b.Key);
        });
        return found;
    }
}

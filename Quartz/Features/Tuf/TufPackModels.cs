#nullable enable
namespace Quartz.Features.Tuf;
public enum TufPackSort { Recent, Name, Levels }
public enum TufPackLevelSort { PackOrder, Difficulty, Clears }
public enum TufPackListState { Idle, Loading, Ready, Empty, Error }
public sealed class TufPackItem {
    public long Key { get; }
    public string Name { get; }
    public TufLevel? Level { get; }
    public IReadOnlyList<TufPackItem> Children { get; }
    public int LevelCount { get; }
    public bool IsFolder => Level == null;
    public TufPackItem(long key, TufLevel level) {
        Key = key;
        Name = level.Song;
        Level = level;
        Children = Array.Empty<TufPackItem>();
        LevelCount = 1;
    }
    public TufPackItem(long key, string name, IReadOnlyList<TufPackItem> children) {
        Key = key;
        Name = name;
        Children = children;
        LevelCount = children.Sum(c => c.LevelCount);
    }
}
public sealed class TufPack {
    public string Id { get; }
    public string Name { get; }
    public string Owner { get; }
    public int LevelCount { get; }
    public int Favorites { get; }
    public IReadOnlyList<string> Preview { get; }
    public string IconUrl { get; set; } = "";
    public int FirstLevelId { get; set; }
    public TufPack(string id, string name, string owner, int levelCount, int favorites, IReadOnlyList<string> preview) {
        Id = id;
        Name = name;
        Owner = owner;
        LevelCount = levelCount;
        Favorites = favorites;
        Preview = preview;
    }
}
public sealed class TufPacksPage {
    public IReadOnlyList<TufPack> Results { get; }
    public int Total { get; }
    public TufPacksPage(IReadOnlyList<TufPack> results, int total) {
        Results = results ?? Array.Empty<TufPack>();
        Total = total;
    }
}
public sealed class TufDifficultyDictionary {
    private readonly Dictionary<int, (string Name, string Color, int Rank)> map;
    public TufDifficultyDictionary(Dictionary<int, (string, string, int)> map) => this.map = map;
    public static TufDifficultyDictionary Empty => new(new());
    public (string Name, string Color) Resolve(int diffId) =>
        map.TryGetValue(diffId, out (string Name, string Color, int Rank) value)
            ? (value.Name, value.Color) : ("Unranked", "#FFFFFF");
    public int RankOf(int diffId) =>
        map.TryGetValue(diffId, out (string Name, string Color, int Rank) value) ? value.Rank : 0;
}

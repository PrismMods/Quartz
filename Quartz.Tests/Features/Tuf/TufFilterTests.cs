using Quartz.Features.Tuf;
using static Asserts;
static class TufFilterTests {
    public static void TestPersistedPreferences() {
        TufSettings source = new() {
            Sort = (int)TufSort.Likes,
            Ascending = true,
            MinDifficultyIndex = 50,
            MaxDifficultyIndex = 10,
            QuantumEnabled = false,
            QuantumMinIndex = 8,
            QuantumMaxIndex = 2,
            SpecialDifficulties = ["Impossible", "unknown"]
        };
        TufSettings loaded = new();
        loaded.Deserialize(source.Serialize());
        TufDifficultyFilter filter = loaded.GetDifficultyFilter();
        Assert(loaded.GetSort() == TufSort.Likes && loaded.Ascending, "sort settings round-trip");
        Assert(filter.MinIndex == 10 && filter.MaxIndex == 50, "persisted PGU range normalizes");
        Assert(filter.IsSelected("Impossible") && !filter.HasQuantum, "specials persist without enabling quantum");
        Assert(loaded.QuantumMinIndex == 2 && loaded.QuantumMaxIndex == 8,
            "disabled quantum endpoints remain available");
        loaded.QuantumEnabled = true;
        filter = loaded.GetDifficultyFilter();
        Assert(filter.QuantumMinIndex == 2 && filter.QuantumMaxIndex == 8,
            "restored quantum uses saved endpoints");
    }
    public static void TestDifficultyFilterContract() {
        Assert(TufDifficultyFilter.RankedNames.Count == 60, "PGU range has 60 steps");
        Assert(TufDifficultyFilter.RankedNames[0] == "P1", "PGU begins at P1");
        Assert(TufDifficultyFilter.RankedNames[19] == "P20", "P band ends at P20");
        Assert(TufDifficultyFilter.RankedNames[20] == "G1", "G band begins at G1");
        Assert(TufDifficultyFilter.RankedNames[59] == "U20", "PGU ends at U20");
        TufDifficultyFilter clamped = new(-20, 100);
        Assert(clamped.MinIndex == 0 && clamped.MaxIndex == 59, "range clamps to PGU bounds");
        TufDifficultyFilter swapped = new(50, 10);
        Assert(swapped.MinIndex == 10 && swapped.MaxIndex == 50, "reversed range swaps endpoints");
        int g5 = TufDifficultyFilter.RankedNames.ToList().IndexOf("G5");
        TufDifficultyFilter exact = new(g5, g5);
        Assert(exact.MinName == "G5" && exact.MaxName == "G5", "exact ranked difficulty supported");
        TufDifficultyFilter selected = exact.Toggle("Impossible").Toggle("GQ0 (G1~G4)");
        Assert(selected.IsSelected("Impossible"), "special difficulty selected");
        Assert(selected.IsSelected("GQ0 (G1~G4)"), "quantum difficulty selected");
        Assert(!selected.Equals(exact), "filter equality detects stale request snapshot");
        Assert(TufDifficultyFilter.AllRanked.Equals(new(0, 59)), "reset restores all ranked and no specials");
        Assert(new TufPage(Array.Empty<TufLevel>(), true, 50).ConsumedCount == 50,
            "pagination preserves raw API records consumed before filtering");
    }
    public static void TestApiDifficultyQuery() {
        string defaultPath = TufApiQuery.BuildPath("", TufSort.Recent, false, 0,
            TufDifficultyFilter.AllRanked);
        Assert(defaultPath == "v2/database/levels?limit=50&offset=0&query=&pguRange=P1,U20"
            + "&sort=RECENT_DESC&deletedFilter=hide", "default API contract is exact");
        int g5 = TufDifficultyFilter.RankedNames.ToList().IndexOf("G5");
        TufDifficultyFilter exact = new TufDifficultyFilter(g5, g5)
            .Toggle("Censored").Toggle("GQ0 (G1~G4)");
        string path = TufApiQuery.BuildPath("a b", TufSort.Difficulty, true, -5, exact);
        Assert(path == "v2/database/levels?limit=50&offset=0&query=a%20b&pguRange=G5,G5"
            + "&sort=DIFF_ASC&deletedFilter=hide"
            + "&specialDifficulties=Censored%2CGQ0%20%28G1~G4%29",
            "filtered API contract is exact");
        Assert(path.Contains("offset=0"), "API offset clamped");
        Assert(path.Contains("query=a%20b"), "search query encoded");
        Assert(path.Contains("pguRange=G5,G5"), "exact API difficulty emitted");
        Assert(path.Contains("sort=DIFF_ASC"), "sort emitted");
        Assert(path.Contains("specialDifficulties=Censored%2CGQ0%20%28G1~G4%29"),
            "special and quantum names encoded as one list");
    }
    public static void TestQuantumRange() {
        IReadOnlyList<string> q = TufDifficultyFilter.QuantumNames;
        TufDifficultyFilter f = new TufDifficultyFilter(0, 59).Toggle("Censored").WithQuantumRange(1, 3);
        Assert(f.HasQuantum, "quantum range marks filter as quantum");
        Assert(f.IsSelected(q[1]) && f.IsSelected(q[2]) && f.IsSelected(q[3]), "range selects endpoints and interior");
        Assert(!f.IsSelected(q[0]) && !f.IsSelected(q[4]), "range excludes outside quantum diffs");
        Assert(f.IsSelected("Censored"), "quantum range preserves special selection");
        Assert(f.QuantumMinIndex == 1 && f.QuantumMaxIndex == 3, "quantum endpoints round-trip");
        TufDifficultyFilter swapped = TufDifficultyFilter.AllRanked.WithQuantumRange(9, 2);
        Assert(swapped.QuantumMinIndex == 2 && swapped.QuantumMaxIndex == 9, "reversed quantum range swaps");
        TufDifficultyFilter clamped = TufDifficultyFilter.AllRanked.WithQuantumRange(-5, 999);
        Assert(clamped.QuantumMinIndex == 0 && clamped.QuantumMaxIndex == q.Count - 1, "quantum range clamps to bounds");
        TufDifficultyFilter cleared = f.WithoutQuantum();
        Assert(!cleared.HasQuantum, "cleared quantum reports disabled");
        Assert(cleared.IsSelected("Censored"), "clearing quantum keeps specials");
        Assert(cleared.QuantumMinIndex == 0 && cleared.QuantumMaxIndex == q.Count - 1,
            "disabled quantum reports default endpoints");
        string path = TufApiQuery.BuildPath("", TufSort.Recent, false, 0,
            TufDifficultyFilter.AllRanked.WithQuantumRange(0, 0));
        Assert(path.Contains("specialDifficulties=Qq"), "quantum range emits API special difficulty");
    }
}

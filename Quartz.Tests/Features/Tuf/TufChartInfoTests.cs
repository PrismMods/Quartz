#nullable enable
using Quartz.Features.Tuf;
using static Asserts;
static class TufChartInfoTests {
    public static void TestReadsSettingsAfterChartData() {
        string temp = NewTemp();
        try {
            string chart = Path.Combine(temp, "main.adofai");
            File.WriteAllText(chart, """
                {"angleData":[0,0,90,180,0,0,0,180,90,0,0,0,0,0,0,0],
                 "settings":{"version":15,"artist":"Frums","song":"multi_arm","author":"Zagon","bpm":200},
                 "actions":[]}
                """);
            TufChartInfo? info = TufChartInfo.Read(chart);
            Assert(info != null, "settings found after a long angleData array");
            Assert(info!.Song == "multi_arm", "song read");
            Assert(info.Artist == "Frums", "artist read");
            Assert(info.Creator == "Zagon", "author read as creator");
            Assert(!info.IsEmpty, "populated info is not empty");
        } finally { Cleanup(temp); }
    }
    public static void TestRejectsUnreadableCharts() {
        string temp = NewTemp();
        try {
            Assert(TufChartInfo.Read(null) == null, "null path rejected");
            Assert(TufChartInfo.Read("") == null, "empty path rejected");
            Assert(TufChartInfo.Read(Path.Combine(temp, "missing.adofai")) == null, "missing file rejected");
            string junk = Path.Combine(temp, "junk.adofai");
            File.WriteAllText(junk, "this is not json at all");
            Assert(TufChartInfo.Read(junk) == null, "malformed chart rejected");
            string blank = Path.Combine(temp, "blank.adofai");
            File.WriteAllText(blank, """{"settings":{"song":"","artist":"","author":""}}""");
            Assert(TufChartInfo.Read(blank) == null, "settings without any names is treated as no info");
            string noSettings = Path.Combine(temp, "nosettings.adofai");
            File.WriteAllText(noSettings, """{"angleData":[0,0],"actions":[]}""");
            Assert(TufChartInfo.Read(noSettings) == null, "chart without settings rejected");
        } finally { Cleanup(temp); }
    }
    public static void TestEntryMetadataMerge() {
        TufInstallEntry entry = new() { Id = 5, Folder = "/tmp/5" };
        Assert(entry.NeedsInfo, "a bare adopted entry needs info");
        Assert(!entry.ApplyChart(null), "no chart info is not a change");
        string temp = NewTemp();
        try {
            string chart = Path.Combine(temp, "main.adofai");
            File.WriteAllText(chart, """{"settings":{"song":"Song","artist":"Artist","author":"Charter"}}""");
            Assert(entry.ApplyChart(TufChartInfo.Read(chart)), "chart info fills an empty entry");
            Assert(entry.Song == "Song" && entry.Artist == "Artist" && entry.Creator == "Charter", "chart fields copied");
            Assert(entry.NeedsInfo, "difficulty still missing after a local read");
            Assert(!entry.ApplyChart(TufChartInfo.Read(chart)), "re-applying the same chart changes nothing");
        } finally { Cleanup(temp); }
        TufLevel level = new(5, "Api Song", "Api Artist", "Api Charter", "U5", "#613E8C", 34, 20,
            new Uri("https://api.tuforums.com/cdn/abc")) { VideoLink = "https://youtu.be/x" };
        Assert(entry.ApplyLevel(level), "api info updates the entry");
        Assert(entry.Song == "Api Song", "api wins over the local chart name");
        Assert(entry.Difficulty == "U5" && entry.DifficultyColor == "#613E8C", "difficulty filled");
        Assert(entry.Clears == 34 && entry.Likes == 20, "counts filled");
        Assert(entry.DownloadUrl.Length > 0 && entry.ToLevel().DownloadUri != null, "download url filled and trusted");
        Assert(!entry.NeedsInfo, "entry no longer needs info");
        Assert(!entry.ApplyLevel(level), "re-applying the same level changes nothing");
        Assert(!entry.ApplyLevel(new TufLevel(6, "Other", "Other", "Other", "P1", "#FFFFFF", 0, 0, null)),
            "a level for another id is ignored");
    }
    static string NewTemp() {
        string temp = Path.Combine(Path.GetTempPath(), "quartz-chart-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        return temp;
    }
    static void Cleanup(string temp) {
        try { Directory.Delete(temp, true); } catch { }
    }
}

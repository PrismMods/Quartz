using System.Text;
using Quartz.Features.Tuf;
using static Asserts;

// Fixtures mirror the real api.tuforums.com payloads captured 2026-07-11:
// pack ids are opaque strings ("RCAXIAv9"), list rows carry levelCount, and tree
// levels have no flat creator — charters live in levelCredits[].creator.name.
static class TufPackParseTests {
    public static void TestPackListParsing() {
        const string json = """
        {
          "packs": [
            {
              "id": "RCAXIAv9",
              "name": "FAINT Full Collection",
              "levelCount": 24,
              "favoritesCount": 7,
              "packOwner": { "nickname": "makk.borealis", "username": "makk" },
              "packItems": [
                { "levelId": 5358, "referencedLevel": { "song": "REBIRTH" } },
                { "levelId": 5644, "referencedLevel": { "song": "humanity" } }
              ]
            },
            { "id": "bad id!", "name": "rejected" },
            { "id": 42, "name": "numeric id tolerated", "totalLevelCount": "3", "favoritesCount": "oops" }
          ],
          "total": 147
        }
        """;
        TufPacksPage page = TufPackApiClient.ParsePacks(Encoding.UTF8.GetBytes(json));
        Assert(page.Total == 147, "total read");
        Assert(page.Results.Count == 2, "invalid pack id skipped");
        TufPack pack = page.Results[0];
        Assert(pack.Id == "RCAXIAv9", "string id preserved");
        Assert(pack.Name == "FAINT Full Collection", "name read");
        Assert(pack.Owner == "makk.borealis", "owner nickname preferred");
        Assert(pack.LevelCount == 24, "levelCount read");
        Assert(pack.Favorites == 7, "favorites read");
        Assert(pack.Preview.Count == 2 && pack.Preview[0] == "REBIRTH", "preview songs read");
        TufPack numeric = page.Results[1];
        Assert(numeric.Id == "42", "numeric id read as string");
        Assert(numeric.LevelCount == 3, "totalLevelCount fallback with string number");
        Assert(numeric.Favorites == 0, "garbage count swallowed to zero");

        Assert(TufPackApiClient.IsValidPackId("RCAXIAv9"), "link code id accepted");
        Assert(!TufPackApiClient.IsValidPackId(""), "empty id rejected");
        Assert(!TufPackApiClient.IsValidPackId("a/../b"), "path chars rejected");
        Assert(!TufPackApiClient.IsValidPackId(new string('x', 65)), "oversized id rejected");
    }

    public static void TestPackTreeParsing() {
        const string json = """
        {
          "id": "RCAXIAv9",
          "items": [
            {
              "type": "folder", "sortOrder": 1, "name": "Chapter 2",
              "children": [
                {
                  "type": "level", "sortOrder": 0, "levelId": 5644,
                  "referencedLevel": {
                    "id": 5644, "song": "humanity", "artist": "Qyubey", "diffId": 4,
                    "clears": 9,
                    "dlLink": "https://api.tuforums.com/cdn/abc",
                    "levelCredits": [
                      { "role": "vfxer", "creator": { "name": "kernby" } },
                      { "role": "charter", "creator": { "name": "hotduck" } }
                    ]
                  }
                }
              ]
            },
            {
              "type": "level", "sortOrder": 0, "levelId": 5358,
              "referencedLevel": {
                "id": 5358, "song": "REBIRTH", "artist": "Dictate & Silentroom", "diffId": 6,
                "clears": 14,
                "dlLink": "https://evil.example.com/zip",
                "levelCredits": [
                  { "role": "vfxer", "creator": { "name": "kernby" } }
                ]
              }
            },
            { "type": "level", "sortOrder": 2, "levelId": 5358,
              "referencedLevel": { "id": 5358, "song": "REBIRTH dup" } }
          ]
        }
        """;
        var difficulties = new TufDifficultyDictionary(new() {
            [4] = ("G1", "#FF0000", 21),
            [6] = ("P6", "#00BBFF", 6)
        });
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        IReadOnlyList<TufLevel> levels = TufPackApiClient.ParsePackLevels(bytes, difficulties);
        Assert(levels.Count == 2, "duplicate level id removed");
        Assert(levels[0].Id == 5358 && levels[1].Id == 5644, "sortOrder respected across folder and level");
        Assert(levels[0].DownloadUri == null, "non-TUF download host stripped");
        Assert(levels[1].DownloadUri != null, "TUF CDN download kept");
        Assert(levels[1].Creator == "hotduck", "charter credit preferred over vfxer");
        Assert(levels[0].Creator == "kernby", "non-charter credit used as fallback");
        Assert(levels[1].Difficulty == "G1" && levels[1].DifficultyColor == "#FF0000", "diffId resolved via dictionary");
        Assert(levels[1].DifficultyRank == 21, "difficulty rank read from dictionary");
        Assert(levels[0].Clears == 14, "clears read");
        Assert(levels[0].Likes == 0, "absent likes default to zero");

        IReadOnlyList<TufPackItem> items = TufPackApiClient.ParsePackItems(bytes, difficulties);
        Assert(items.Count == 2, "tree keeps root order without the duplicate");
        Assert(!items[0].IsFolder && items[0].Level!.Id == 5358, "root level first by sortOrder");
        TufPackItem folder = items[1];
        Assert(folder.IsFolder && folder.Name == "Chapter 2", "folder node preserved with name");
        Assert(folder.Children.Count == 1 && folder.Children[0].Level!.Id == 5644, "folder children preserved");
        Assert(folder.LevelCount == 1, "folder level count is recursive");
    }
}

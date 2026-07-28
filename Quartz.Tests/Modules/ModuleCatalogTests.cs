using Quartz.Modules;
using static Asserts;
static class ModuleCatalogTests {
    private const string Sha = "b8f2bf1448f5dff417430fb2c7fcc5ebbcb161e764d66e51df043a971c892007";
    private const string ManifestSha = "b8f59b77faecaa8840831221a9cec90d649c653edca27abd2f32d70b98bf0de7";
    private const string BundleSha = "804e5bb02a0e0930e1ebd5e59e381d3c35079c716c49ff6c436005d118467d36";
    private static string Catalog(string bundle, string entryExtras) => $$"""
        { "schema": 1, "core": { "version": "2.0.0", "abi": 1, "tag": "v2.0.0-alpha-95" },
          {{bundle}}
          "groups": [ { "key": "overlay", "title": "Overlay", "order": 10 } ],
          "modules": [ { "id": "keyviewer", "name": "Key Viewer", "group": "overlay", "order": 20,
                         "coreAbi": 1, "size": 47616, "sha256": "{{Sha}}",
                         "manifestSha256": "{{ManifestSha}}"{{entryExtras}} } ] }
        """;
    private const string BundleBlock = $$"""
        "bundle": { "assetName": "QuartzModules.zip", "size": 611492, "sha256": "{{BundleSha}}",
                    "url": "https://github.com/PrismMods/Quartz/releases/download/v2.0.0-alpha-95/QuartzModules.zip" },
        """;
    public static void TestBundleIsReadFromTheCatalog() {
        ModuleCatalog c = ModuleCatalog.Parse(Catalog(BundleBlock, ""), out string error);
        Assert(c != null && error == null, "a bundle catalog parses: " + error);
        Assert(c.HasBundle, "a catalog carrying a bundle url and sha256 offers the bundle");
        Assert(c.BundleSize == 611492 && c.BundleSha256 == BundleSha, "bundle size and checksum are read");
        Assert(c.BundleUrl.EndsWith("QuartzModules.zip", StringComparison.Ordinal), "the bundle url is read");
    }
    public static void TestModulesNoLongerNeedTheirOwnUrls() {
        ModuleCatalog c = ModuleCatalog.Parse(Catalog(BundleBlock, ""), out _);
        Assert(c?.Find("keyviewer") != null, "an entry with no url of its own still reaches the list");
        Assert(c.Find("keyviewer").Sha256 == Sha, "its checksum survives, so extraction can still be verified");
    }
    public static void TestALooseUrlCatalogStillWorks() {
        const string extras = """
            , "url": "https://example.invalid/keyviewer.qmod",
              "manifestUrl": "https://example.invalid/keyviewer.qmod.json"
            """;
        ModuleCatalog c = ModuleCatalog.Parse(Catalog("", extras), out string error);
        Assert(c != null && error == null, "a catalog with no bundle still parses: " + error);
        Assert(!c.HasBundle, "no bundle block means no bundle to download");
        Assert(c.Find("keyviewer")?.Url == "https://example.invalid/keyviewer.qmod",
            "per-module urls are still honoured when that is all the catalog offers");
    }
    public static void TestAnUnusableBundleIsNotOffered() {
        const string noSha = """
            "bundle": { "url": "https://example.invalid/QuartzModules.zip", "size": 10 },
            """;
        Assert(!ModuleCatalog.Parse(Catalog(noSha, ""), out _).HasBundle,
            "a bundle without a checksum is never downloaded");
        const string shortSha = """
            "bundle": { "url": "https://example.invalid/QuartzModules.zip", "sha256": "abc123" },
            """;
        Assert(!ModuleCatalog.Parse(Catalog(shortSha, ""), out _).HasBundle,
            "a malformed checksum is never downloaded");
        const string noUrl = $$"""
            "bundle": { "sha256": "{{BundleSha}}", "size": 10 },
            """;
        Assert(!ModuleCatalog.Parse(Catalog(noUrl, ""), out _).HasBundle,
            "a bundle with nowhere to download from is never offered");
    }
}

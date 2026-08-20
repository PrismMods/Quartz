using Quartz.Features.Discord;
using static Asserts;
static class DiscordVoiceTests {
    const string Sha = "84569f5a6c41e737cec90ea87c5f1a664b3d7ffc885f65cb96d25b60be09c6a7";
    static string Sample => """
    {
      "schema": 1,
      "version": "test-1",
      "platforms": {
        "osx-arm64": { "packages": [
          { "name": "opus", "url": "https://example.invalid/a.nupkg", "sha256": "SHA",
            "entry": "shared/osx-arm64/native/libopus.dylib", "file": "libopus.dylib" },
          { "name": "sodium", "url": "https://example.invalid/b.nupkg", "sha256": "SHA",
            "entry": "runtimes/osx-arm64/native/libsodium.dylib", "file": "libsodium.dylib" }
        ] },
        "win-arm64": { "packages": [
          { "name": "opus", "url": "https://example.invalid/a.nupkg", "sha256": "SHA",
            "entry": "shared/win-arm64/native/libopus.dll", "file": "libopus.dll" }
        ] }
      }
    }
    """.Replace("SHA", Sha);
    public static void TestParsesPackagesForThePlatform() {
        VoiceNativeEntry entry = VoiceManifest.Parse(Sample, "osx-arm64");
        Assert(entry != null, "the osx-arm64 entry must parse");
        Assert(entry.Packages.Count == 2, $"expected 2 packages, got {entry.Packages.Count}");
        Assert(entry.Version == "test-1", "the entry inherits the manifest version");
        Assert(entry.Has("opus") && entry.Has("sodium"), "both libraries must be present");
        Assert(!entry.Has("dave"), "dave must not be reported when it is not listed");
        Assert(entry.Packages[0].File == "libopus.dylib", "the destination file name is read");
    }
    public static void TestRejectsIncompleteOrUnhashedPackages() {
        Assert(VoiceManifest.Parse(Sample, "linux-x64") == null, "an absent platform must not resolve");
        Assert(VoiceManifest.Parse(Sample, null) == null, "a null rid must not resolve");
        Assert(VoiceManifest.Parse("{}", "osx-arm64") == null, "a manifest with no platforms must not resolve");
        string noHash = Sample.Replace(Sha, "abc");
        Assert(
            VoiceManifest.Parse(noHash, "osx-arm64") == null,
            "a package whose checksum is not a full SHA-256 must be discarded"
        );
        string noUrl = Sample.Replace("https://example.invalid/a.nupkg", "");
        VoiceNativeEntry partial = VoiceManifest.Parse(noUrl, "osx-arm64");
        Assert(partial != null && !partial.Has("opus"), "a package with no url must be discarded");
    }
    public static void TestTheShippedManifestCoversEveryPlatform() {
        string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string json = File.ReadAllText(Path.Combine(repo, "modules", "Discord", "voice-natives.json"));
        (string Rid, string Extension, bool Dave)[] expected = [
            ("win-x64", "dll", true),
            ("win-arm64", "dll", false),
            ("osx-x64", "dylib", true),
            ("osx-arm64", "dylib", true),
            ("linux-x64", "so", true),
            ("linux-arm64", "so", true),
        ];
        foreach((string rid, string extension, bool dave) in expected) {
            VoiceNativeEntry entry = VoiceManifest.Parse(json, rid);
            Assert(entry != null, $"the shipped manifest must cover {rid}");
            Assert(entry.Has("opus") && entry.Has("sodium"), $"{rid} needs both opus and sodium");
            Assert(entry.Has("dave") == dave, $"{rid} dave availability should be {dave}");
            foreach(VoiceNativePackage package in entry.Packages) {
                Assert(package.Sha256.Length == 64, $"{rid}/{package.Name} must pin a full SHA-256");
                foreach(char c in package.Sha256)
                    Assert(
                        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'),
                        $"{rid}/{package.Name} checksum must be lowercase hex"
                    );
                Assert(
                    package.Url.StartsWith("https://api.nuget.org/", StringComparison.Ordinal),
                    $"{rid}/{package.Name} must come from nuget.org over https"
                );
                Assert(
                    package.Entry.Contains("/" + rid + "/", StringComparison.Ordinal),
                    $"{rid}/{package.Name} must extract that platform's binary, not another's"
                );
                Assert(
                    package.File.EndsWith("." + extension, StringComparison.Ordinal),
                    $"{rid}/{package.Name} must install a .{extension}"
                );
            }
        }
    }
    public static void TestHashMatchesTheKnownEmptyDigest() {
        using MemoryStream empty = new();
        Assert(
            VoiceManifest.HashOf(empty) == "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "SHA-256 of no bytes must match the published constant"
        );
    }
    public static void TestHexIsLowercaseAndPadded() {
        Assert(VoiceManifest.ToHex([0x00, 0x0f, 0xff]) == "000fff", "each byte renders as two lowercase hex digits");
        Assert(VoiceManifest.ToHex([]) == "", "an empty digest renders empty");
        Assert(VoiceManifest.ToHex(null) == "", "a null digest renders empty");
    }
}

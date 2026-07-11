using System.IO.Compression;
using System.Net;
using Quartz.Features.Tuf;
using static Asserts;

static class TufSecurityTests {
    public static void TestInputAndNetworkPolicy() {
        string noisy = "  hello\t\r\n world  ";
        Assert(TufInput.NormalizeQuery(noisy) == "hello world", "query whitespace collapsed");
        Assert(TufInput.NormalizeQuery(new string('x', 200)).Length == 128, "query capped");
        Assert(TufInput.NormalizeColor("#aB12f0") == "#AB12F0", "color normalized");
        Assert(TufInput.NormalizeColor("red") == "#FFFFFF", "invalid color replaced");
        Assert(TufNetworkPolicy.IsAllowedDownloadUri(new Uri("https://api.tuforums.com/cdn/file")), "TUF API CDN accepted");
        Assert(TufNetworkPolicy.IsAllowedDownloadUri(new Uri("https://cdn.tuforums.com/zips/file.zip")), "TUF CDN accepted");
        Assert(!TufNetworkPolicy.IsAllowedDownloadUri(new Uri("http://cdn.tuforums.com/file")), "HTTP rejected");
        Assert(!TufNetworkPolicy.IsAllowedDownloadUri(new Uri("https://user@cdn.tuforums.com/file")), "userinfo rejected");
        Assert(!TufNetworkPolicy.IsAllowedDownloadUri(new Uri("https://127.0.0.1/file")), "IP literal rejected");
        Assert(!TufNetworkPolicy.IsAllowedDownloadUri(new Uri("https://example.com/file")), "unknown host rejected");
        Assert(TufNetworkPolicy.IsNonPublic(IPAddress.Parse("10.0.0.1")), "private IPv4 rejected");
        Assert(TufNetworkPolicy.IsNonPublic(IPAddress.Parse("fe80::1")), "link-local IPv6 rejected");
        Assert(!TufNetworkPolicy.IsNonPublic(IPAddress.Parse("1.1.1.1")), "public IPv4 accepted");
    }

    public static void TestArchiveSafetyAndSelection() {
        string temp = Path.Combine(Path.GetTempPath(), "quartz-tuf-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            string traversal = Path.Combine(temp, "traversal.zip");
            MakeZip(traversal, zip => Write(zip, "../escape.adofai", "bad"));
            AssertThrows(() => TufArchive.Extract(traversal, Path.Combine(temp, "out")), "traversal rejected");
            Assert(!File.Exists(Path.Combine(temp, "escape.adofai")), "traversal wrote no file");

            string symlink = Path.Combine(temp, "symlink.zip");
            MakeZip(symlink, zip => {
                ZipArchiveEntry entry = zip.CreateEntry("link.adofai");
                entry.ExternalAttributes = unchecked((int)0xA0000000);
            });
            AssertThrows(() => TufArchive.Extract(symlink, Path.Combine(temp, "links")), "symlink rejected");

            string many = Path.Combine(temp, "many.zip");
            MakeZip(many, zip => {
                for(int i = 0; i <= TufArchive.MaxEntries; i++) zip.CreateEntry("d/" + i);
            });
            AssertThrows(() => TufArchive.Extract(many, Path.Combine(temp, "many")), "entry count capped");

            string valid = Path.Combine(temp, "valid.zip");
            MakeZip(valid, zip => {
                Write(zip, "charts/large.adofai", new string('x', 100));
                Write(zip, "main.adofai", "main");
                Write(zip, "backup.adofai", new string('x', 500));
            });
            string extracted = Path.Combine(temp, "valid");
            TufArchive.Extract(valid, extracted);
            string? selected = TufArchive.SelectChart(extracted);
            Assert(Path.GetFileName(selected) == "main.adofai", "main chart preferred");
            Assert(TufArchive.IsChartUnderRoot(selected, extracted), "selected chart contained by root");
            Assert(!TufArchive.IsChartUnderRoot(Path.Combine(temp, "outside.adofai"), extracted), "outside chart rejected");

            string multi = Path.Combine(temp, "multi.zip");
            MakeZip(multi, zip => {
                Write(zip, "alternate.adofai", new string('x', 200));
                Write(zip, "target.adofai", "target");
            });
            string multiExtracted = Path.Combine(temp, "multi");
            TufArchive.Extract(multi, multiExtracted);
            Assert(Path.GetFileName(TufArchive.SelectChart(multiExtracted, "target.zip")) == "target.adofai",
                "archive-named chart preferred in multi-chart download");
            Assert(Path.GetFileName(TufArchive.SelectChart(multiExtracted)) == "alternate.adofai",
                "largest chart is deterministic multi-chart fallback");
            IReadOnlyList<string> charts = TufArchive.ListCharts(multiExtracted);
            Assert(charts.Count == 2, "chart list finds every playable chart");
            Assert(Path.GetFileName(charts[0]) == "alternate.adofai" && Path.GetFileName(charts[1]) == "target.adofai",
                "chart list preserves selection preference order");
            Assert(TufArchive.ListCharts(Path.Combine(temp, "does-not-exist")).Count == 0, "missing folder lists no charts");

            string wrapped = Path.Combine(temp, "wrapped.zip");
            MakeZip(wrapped, zip => {
                Write(zip, "Artist - Title/level.adofai", "chart");
                Write(zip, "Artist - Title/song.ogg", "audio");
            });
            string wrappedExtracted = Path.Combine(temp, "wrapped");
            TufArchive.Extract(wrapped, wrappedExtracted);
            TufArchive.FlattenSingleRoot(wrappedExtracted);
            Assert(File.Exists(Path.Combine(wrappedExtracted, "level.adofai")), "wrapper folder flattened to root");
            Assert(!Directory.Exists(Path.Combine(wrappedExtracted, "Artist - Title")), "wrapper folder removed");

            string samename = Path.Combine(temp, "samename.zip");
            MakeZip(samename, zip => Write(zip, "X/X/a.adofai", "chart"));
            string samenameExtracted = Path.Combine(temp, "samename");
            TufArchive.Extract(samename, samenameExtracted);
            TufArchive.FlattenSingleRoot(samenameExtracted);
            Assert(File.Exists(Path.Combine(samenameExtracted, "a.adofai")), "double wrapper with same name flattened");

            string flat = Path.Combine(temp, "flat.zip");
            MakeZip(flat, zip => {
                Write(zip, "root.adofai", "chart");
                Write(zip, "assets/img.png", "img");
            });
            string flatExtracted = Path.Combine(temp, "flat");
            TufArchive.Extract(flat, flatExtracted);
            TufArchive.FlattenSingleRoot(flatExtracted);
            Assert(File.Exists(Path.Combine(flatExtracted, "root.adofai"))
                && File.Exists(Path.Combine(flatExtracted, "assets", "img.png")), "already-flat layout untouched");

            string unicode = Path.Combine(temp, "unicode.zip");
            MakeZip(unicode, zip => {
                Write(zip, "레벨/메인.adofai", "main");
                Write(zip, "레벨/이미지.png", "image");
            });
            string unicodeExtracted = Path.Combine(temp, "unicode");
            TufArchive.Extract(unicode, unicodeExtracted);
            Assert(File.Exists(Path.Combine(unicodeExtracted, "레벨", "메인.adofai")), "unicode chart name preserved");
            Assert(File.Exists(Path.Combine(unicodeExtracted, "레벨", "이미지.png")), "unicode asset name preserved");
            Assert(Path.GetFileName(TufArchive.SelectChart(unicodeExtracted)) == "메인.adofai", "unicode chart selected");
        } finally {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    private static void MakeZip(string path, Action<ZipArchive> write) {
        using FileStream stream = new(path, FileMode.CreateNew);
        using ZipArchive zip = new(stream, ZipArchiveMode.Create);
        write(zip);
    }

    private static void Write(ZipArchive zip, string path, string value) {
        ZipArchiveEntry entry = zip.CreateEntry(path);
        using StreamWriter writer = new(entry.Open());
        writer.Write(value);
    }

    private static void AssertThrows(Action action, string message) {
        try { action(); }
        catch(InvalidDataException) { return; }
        throw new InvalidOperationException(message);
    }
}

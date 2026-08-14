using System.Net;
using Quartz.Features.KeyViewer;
using static Asserts;
static class CssAssetDownloaderTests {
    public static void Run() {
        Assert(CssAssetDownloader.ParseTarget("https://example.com/image.png").Scheme == "https",
            "CSS assets allow public HTTP(S) syntax");
        AssertThrows(() => CssAssetDownloader.ParseTarget("file:///etc/passwd"),
            "CSS assets reject non-network schemes");
        AssertThrows(() => CssAssetDownloader.ParseTarget("https://user:pass@example.com/a"),
            "CSS assets reject URL credentials");
        AssertThrows(() => CssAssetDownloader.EnsurePublicAddresses([IPAddress.Loopback]),
            "CSS assets reject loopback targets");
        AssertThrows(() => CssAssetDownloader.EnsurePublicAddresses([IPAddress.Parse("10.1.2.3")]),
            "CSS assets reject private targets");
        AssertThrows(() => CssAssetDownloader.EnsurePublicAddresses([IPAddress.Parse("169.254.1.2")]),
            "CSS assets reject link-local targets");
        AssertThrows(() => CssAssetDownloader.EnsurePublicAddresses([IPAddress.Parse("8.8.8.8"), IPAddress.Loopback]),
            "CSS assets reject a hostname if any answer is non-public");
        CssAssetDownloader.EnsurePublicAddresses([IPAddress.Parse("8.8.8.8")]);

        Uri redirected = CssAssetDownloader.ResolveRedirect(new Uri("https://example.com/a/b"), new Uri("../c", UriKind.Relative));
        Assert(redirected == new Uri("https://example.com/c"), "CSS asset relative redirects resolve safely");
        AssertThrows(() => CssAssetDownloader.ResolveRedirect(new Uri("https://example.com/a"),
            new Uri("file:///etc/passwd")), "CSS asset redirects revalidate their scheme");

        byte[] exact = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();
        using MemoryStream exactStream = new(exact);
        byte[] copied = CssAssetDownloader.ReadBoundedAsync(exactStream, exact.Length,
            CancellationToken.None).GetAwaiter().GetResult();
        Assert(copied.SequenceEqual(exact), "CSS asset streaming accepts the exact byte cap");
        using MemoryStream oversized = new(new byte[129]);
        AssertThrows(() => CssAssetDownloader.ReadBoundedAsync(oversized, 128,
            CancellationToken.None).GetAwaiter().GetResult(), "CSS asset streaming enforces its byte cap");

        var stalled = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        bool cancellationObserved = false;
        try {
            _ = CssAssetDownloader.AwaitWithCancellationAsync(stalled.Task, canceled.Token)
                .GetAwaiter().GetResult();
        } catch(OperationCanceledException) { cancellationObserved = true; }
        Assert(cancellationObserved, "CSS asset DNS waits honor cancellation");
    }
    private static void AssertThrows(Action action, string message) {
        bool threw = false;
        try { action(); } catch(InvalidDataException) { threw = true; }
        Assert(threw, message);
    }
}

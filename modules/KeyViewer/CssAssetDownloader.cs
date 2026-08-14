#nullable enable
using System.Net;
using Quartz.IO;
using Quartz.Net;
namespace Quartz.Features.KeyViewer;
internal static class CssAssetDownloader {
    internal const int MaxAssetBytes = 16 * 1024 * 1024;
    internal const int MaxRedirects = 5;
    internal static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(30);
    private static readonly SemaphoreSlim Slots = new(4, 4);
    private static readonly HttpClient Http = CreateClient();
    internal static async Task DownloadToFileAsync(string url, string path, CancellationToken token) {
        Uri current = ParseTarget(url);
        await Slots.WaitAsync(token).ConfigureAwait(false);
        try {
            byte[] contents = await DownloadAsync(current, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            AtomicFile.WriteAllBytes(path, contents);
        } finally {
            Slots.Release();
        }
    }
    private static async Task<byte[]> DownloadAsync(Uri initial, CancellationToken token) {
        Uri current = initial;
        for(int redirects = 0; redirects <= MaxRedirects; redirects++) {
            await EnsurePublicTargetAsync(current, token).ConfigureAwait(false);
            using HttpRequestMessage request = new(HttpMethod.Get, current);
            using HttpResponseMessage response = await Http.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            int status = (int)response.StatusCode;
            if(status is >= 300 and < 400) {
                if(redirects == MaxRedirects || response.Headers.Location == null)
                    throw new HttpRequestException("CSS asset download has too many redirects.");
                current = ResolveRedirect(current, response.Headers.Location);
                continue;
            }
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength is long length && length > MaxAssetBytes)
                throw new InvalidDataException($"CSS asset exceeds the {MaxAssetBytes}-byte limit.");
            using Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return await ReadBoundedAsync(input, MaxAssetBytes, token).ConfigureAwait(false);
        }
        throw new HttpRequestException("CSS asset redirect failed.");
    }
    internal static Uri ParseTarget(string url) {
        if(!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            throw new InvalidDataException("CSS asset URL is not absolute.");
        return ValidateTarget(uri);
    }
    internal static Uri ResolveRedirect(Uri current, Uri location) {
        Uri next = location.IsAbsoluteUri ? location : new Uri(current, location);
        return ValidateTarget(next);
    }
    private static Uri ValidateTarget(Uri uri) {
        bool http = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        bool https = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if(!uri.IsAbsoluteUri || (!http && !https) || string.IsNullOrWhiteSpace(uri.DnsSafeHost)
            || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidDataException("CSS assets must use a public HTTP(S) URL without credentials.");
        return uri;
    }
    private static async Task EnsurePublicTargetAsync(Uri uri, CancellationToken token) {
        IPAddress[] addresses;
        if(IPAddress.TryParse(uri.DnsSafeHost, out IPAddress? address)) {
            addresses = [address];
        } else {
            Task<IPAddress[]> lookup = Dns.GetHostAddressesAsync(uri.DnsSafeHost);
            addresses = await AwaitWithCancellationAsync(lookup, token).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
        EnsurePublicAddresses(addresses);
    }
    internal static async Task<T> AwaitWithCancellationAsync<T>(Task<T> operation,
        CancellationToken token) {
        token.ThrowIfCancellationRequested();
        if(operation.IsCompleted) return await operation.ConfigureAwait(false);
        Task canceled = Task.Delay(Timeout.Infinite, token);
        if(await Task.WhenAny(operation, canceled).ConfigureAwait(false) == operation)
            return await operation.ConfigureAwait(false);
        // DNS has no cancellable netstandard API. Observe its eventual failure after
        // releasing the bounded slot so a stuck resolver cannot stall all downloads.
        _ = operation.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        token.ThrowIfCancellationRequested();
        throw new OperationCanceledException(token);
    }
    internal static void EnsurePublicAddresses(IEnumerable<IPAddress> addresses) {
        bool any = false;
        foreach(IPAddress address in addresses) {
            any = true;
            if(NetworkPolicy.IsNonPublic(address))
                throw new InvalidDataException("CSS asset host resolved to a non-public address.");
        }
        if(!any) throw new InvalidDataException("CSS asset host did not resolve.");
    }
    internal static async Task<byte[]> ReadBoundedAsync(Stream input, int maxBytes,
        CancellationToken token) {
        if(maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        using MemoryStream output = new(Math.Min(maxBytes, 64 * 1024));
        byte[] buffer = new byte[64 * 1024];
        while(true) {
            int read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
            if(read == 0) return output.ToArray();
            if(output.Length > maxBytes - read)
                throw new InvalidDataException($"CSS asset exceeds the {maxBytes}-byte limit.");
            output.Write(buffer, 0, read);
        }
    }
    private static HttpClient CreateClient() {
        HttpClient client = new(new HttpClientHandler {
            AllowAutoRedirect = false,
            UseCookies = false
        }) {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-KeyViewer/1.0");
        return client;
    }
}

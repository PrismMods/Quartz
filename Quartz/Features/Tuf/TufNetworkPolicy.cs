#nullable enable
using System.Net;
using Quartz.Net;
namespace Quartz.Features.Tuf;
public static class TufNetworkPolicy {
    public static bool IsAllowedDownloadUri(Uri? uri) => NetworkPolicy.Tuf.IsAllowed(uri);
    public static Task EnsurePublicHostAsync(Uri uri, CancellationToken token) =>
        NetworkPolicy.Tuf.EnsurePublicHostAsync(uri, token);
    public static bool IsOfflineError(Exception error) => NetworkPolicy.IsOfflineError(error);
    public static bool IsNonPublic(IPAddress address) => NetworkPolicy.IsNonPublic(address);
}

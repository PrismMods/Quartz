using System.Net;
using System.Net.Sockets;
namespace Quartz.Net;
public sealed class NetworkPolicy {
    private readonly HashSet<string> hosts;
    private readonly string label;
    public NetworkPolicy(string label, params string[] allowedHosts) {
        this.label = label;
        hosts = new HashSet<string>(allowedHosts, StringComparer.OrdinalIgnoreCase);
    }
    public bool IsAllowed(Uri uri) {
        if(uri == null || !uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps) return false;
        if(!string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) return false;
        if(IPAddress.TryParse(uri.DnsSafeHost, out _)) return false;
        return hosts.Contains(uri.DnsSafeHost);
    }
    public async Task EnsurePublicHostAsync(Uri uri, CancellationToken token) {
        if(!IsAllowed(uri)) throw new InvalidDataException($"URL is not an approved {label} HTTPS URL.");
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if(addresses.Length == 0 || addresses.Any(IsNonPublic))
            throw new InvalidDataException("Host resolved to a non-public address.");
    }
    public static readonly NetworkPolicy Tuf = new("TUF", "api.tuforums.com", "cdn.tuforums.com");
    public static readonly NetworkPolicy Github = new(
        "GitHub",
        "github.com",
        "api.github.com",
        "objects.githubusercontent.com",
        "raw.githubusercontent.com",
        "release-assets.githubusercontent.com"
    );
    public static bool IsOfflineError(Exception error) {
        for(Exception current = error; current != null; current = current.InnerException) {
            if(current is SocketException or WebException or TimeoutException or IOException) return true;
        }
        return false;
    }
    public static bool IsNonPublic(IPAddress address) {
        if(IPAddress.IsLoopback(address)) return true;
        if(address.IsIPv4MappedToIPv6) return IsNonPublic(address.MapToIPv4());
        byte[] b = address.GetAddressBytes();
        if(address.AddressFamily == AddressFamily.InterNetwork) {
            return b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224
                || (b[0] == 100 && b[1] is >= 64 and <= 127)
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 172 && b[1] is >= 16 and <= 31)
                || (b[0] == 192 && b[1] is 0 or 168)
                || (b[0] == 192 && b[1] == 88 && b[2] == 99)
                || (b[0] == 198 && b[1] is 18 or 19 or 51)
                || (b[0] == 203 && b[1] == 0 && b[2] == 113);
        }
        if(address.AddressFamily != AddressFamily.InterNetworkV6) return true;
        if(address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None)
            || address.Equals(IPAddress.IPv6Loopback) || address.IsIPv6LinkLocal
            || address.IsIPv6Multicast || address.IsIPv6SiteLocal) return true;
        return (b[0] & 0xFE) == 0xFC
            || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8);
    }
}

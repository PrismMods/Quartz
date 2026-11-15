using Newtonsoft.Json.Linq;
using Quartz.Async;
using Quartz.Core;
using System.Net.Http;
using System.Text.RegularExpressions;
namespace Quartz.Addons;
internal static class AddonUpdateCheck {
    internal sealed class Result {
        public string Tag;
        public string Url;
    }
    private static readonly Dictionary<string, Result> cache = new(StringComparer.OrdinalIgnoreCase);
    // Each repo in flight, with every caller waiting on it: a second page's check while the first's fetch runs still hears back.
    private static readonly Dictionary<string, List<(string Version, Action<Result> OnNewer)>> pending = new(StringComparer.OrdinalIgnoreCase);
    private static HttpClient http;
    private static HttpClient Http {
        get {
            if(http == null) {
                http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Quartz-AddonUpdateCheck");
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            }
            return http;
        }
    }
    internal static void Check(string repo, string currentVersion, Action<Result> onNewer) {
#if DEBUG
        if(SelfTestDoor.Installed) {
            SelfTestDoor.UpdateChecks++;
            return;
        }
#endif
        if(string.IsNullOrWhiteSpace(repo) || onNewer == null) return;
        repo = repo.Trim().TrimEnd('/');
        if(!Regex.IsMatch(repo, @"^[\w.-]+/[\w.-]+$")) return;
        if(cache.TryGetValue(repo, out Result cached)) {
            if(IsNewer(cached?.Tag, currentVersion)) onNewer(cached);
            return;
        }
        if(pending.TryGetValue(repo, out var waiting)) {
            waiting.Add((currentVersion, onNewer));
            return;
        }
        pending[repo] = [(currentVersion, onNewer)];
        Fetch(repo);
    }
    private static async void Fetch(string repo) {
        Result result = null;
        try {
            string json = await Http.GetStringAsync($"https://api.github.com/repos/{repo}/releases/latest");
            JObject release = JObject.Parse(json);
            result = new Result {
                Tag = release["tag_name"]?.ToString(),
                Url = release["html_url"]?.ToString() ?? $"https://github.com/{repo}/releases",
            };
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        MainThread.Enqueue(() => {
            cache[repo] = result;
            if(!pending.TryGetValue(repo, out var waiting)) return;
            pending.Remove(repo);
            if(result == null) return;
            foreach((string version, Action<Result> onNewer) in waiting) {
                if(!IsNewer(result.Tag, version)) continue;
                try {
                    onNewer(result);
                } catch(Exception e) {
                    MainCore.Log.Err($"[{nameof(AddonUpdateCheck)}] a caller's result handler threw: {e}");   // the other callers still hear back
                    Diag.Ignore(e);
                }
            }
        });
    }
    private static bool IsNewer(string tag, string current) {
        Version latest = Parse(tag);
        Version installed = Parse(current);
        return latest != null && installed != null && latest > installed;
    }
    private static Version Parse(string text) {
        if(string.IsNullOrWhiteSpace(text)) return null;
        Match match = Regex.Match(text, @"\d+(\.\d+){1,3}");
        return match.Success && Version.TryParse(match.Value, out Version version) ? version : null;
    }
}

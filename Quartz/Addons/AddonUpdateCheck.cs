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
    private static readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);
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
        if(string.IsNullOrWhiteSpace(repo) || onNewer == null) return;
        repo = repo.Trim().TrimEnd('/');
        if(!Regex.IsMatch(repo, @"^[\w.-]+/[\w.-]+$")) return;
        if(cache.TryGetValue(repo, out Result cached)) {
            if(IsNewer(cached?.Tag, currentVersion)) onNewer(cached);
            return;
        }
        if(!pending.Add(repo)) return;
        Fetch(repo, currentVersion, onNewer);
    }
    private static async void Fetch(string repo, string currentVersion, Action<Result> onNewer) {
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
            pending.Remove(repo);
            if(result != null && IsNewer(result.Tag, currentVersion)) onNewer(result);
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

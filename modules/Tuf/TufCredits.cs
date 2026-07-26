#nullable enable
using Newtonsoft.Json.Linq;
namespace Quartz.Features.Tuf;
public static class TufCredits {
    public static string? Extract(JToken? level) {
        if(level == null) return null;
        string? flat = level.Value<string>("creator") ?? level.Value<string>("charter");
        if(!string.IsNullOrWhiteSpace(flat)) return flat;
        if(level["levelCredits"] is not JArray credits) return null;
        List<string> charters = [];
        List<string> others = [];
        foreach(JToken credit in credits) {
            string? name = credit["creator"]?.Value<string>("name");
            if(string.IsNullOrWhiteSpace(name)) continue;
            string role = credit.Value<string>("role") ?? "";
            (role.Contains("charter", StringComparison.OrdinalIgnoreCase) ? charters : others).Add(name!);
        }
        List<string> chosen = charters.Count > 0 ? charters : others;
        if(chosen.Count == 0) return null;
        return chosen.Count <= 3 ? string.Join(" & ", chosen) : string.Join(" & ", chosen.Take(3)) + " …";
    }
}

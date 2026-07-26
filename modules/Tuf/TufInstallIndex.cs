#nullable enable
using Newtonsoft.Json.Linq;
using Quartz.IO.Interface;
namespace Quartz.Features.Tuf;
public sealed class TufInstallEntry {
    public int Id;
    public string Song = "";
    public string Artist = "";
    public string Creator = "";
    public string Difficulty = "";
    public string DifficultyColor = "#FFFFFF";
    public int Clears;
    public int Likes;
    public string Folder = "";
    public string DownloadUrl = "";
    public string VideoLink = "";
    public long InstalledAtUtc;
    public bool NeedsInfo => string.IsNullOrEmpty(Song) || string.IsNullOrEmpty(Difficulty);
    public bool ApplyChart(TufChartInfo? info) {
        if(info == null) return false;
        bool changed = false;
        if(string.IsNullOrEmpty(Song) && info.Song.Length > 0) {
            Song = info.Song;
            changed = true;
        }
        if(string.IsNullOrEmpty(Artist) && info.Artist.Length > 0) {
            Artist = info.Artist;
            changed = true;
        }
        if(string.IsNullOrEmpty(Creator) && info.Creator.Length > 0) {
            Creator = info.Creator;
            changed = true;
        }
        return changed;
    }
    public bool ApplyLevel(TufLevel? level) {
        if(level == null || level.Id != Id) return false;
        bool changed = false;
        changed |= Replace(ref Song, level.Song);
        changed |= Replace(ref Artist, level.Artist);
        changed |= Replace(ref Creator, level.Creator);
        changed |= Replace(ref Difficulty, level.Difficulty);
        changed |= Replace(ref DifficultyColor, level.DifficultyColor);
        changed |= Replace(ref VideoLink, level.VideoLink);
        changed |= Replace(ref DownloadUrl, level.DownloadUri?.ToString() ?? "");
        if(Clears != level.Clears) {
            Clears = level.Clears;
            changed = true;
        }
        if(Likes != level.Likes) {
            Likes = level.Likes;
            changed = true;
        }
        return changed;
    }
    private static bool Replace(ref string field, string value) {
        if(string.IsNullOrEmpty(value) || string.Equals(field, value, StringComparison.Ordinal)) return false;
        field = value;
        return true;
    }
    public JObject Serialize() => new() {
        [nameof(Id)] = Id,
        [nameof(Song)] = Song,
        [nameof(Artist)] = Artist,
        [nameof(Creator)] = Creator,
        [nameof(Difficulty)] = Difficulty,
        [nameof(DifficultyColor)] = DifficultyColor,
        [nameof(Clears)] = Clears,
        [nameof(Likes)] = Likes,
        [nameof(Folder)] = Folder,
        [nameof(DownloadUrl)] = DownloadUrl,
        [nameof(VideoLink)] = VideoLink,
        [nameof(InstalledAtUtc)] = InstalledAtUtc
    };
    public static TufInstallEntry? Deserialize(JToken token) {
        try {
            int id = token[nameof(Id)]?.Value<int>() ?? 0;
            if(id <= 0) return null;
            string folder = token[nameof(Folder)]?.Value<string>() ?? "";
            if(string.IsNullOrWhiteSpace(folder)) return null;
            return new TufInstallEntry {
                Id = id,
                Song = TufInput.CapDisplay(token[nameof(Song)]?.Value<string>(), ""),
                Artist = TufInput.CapDisplay(token[nameof(Artist)]?.Value<string>(), ""),
                Creator = TufInput.CapDisplay(token[nameof(Creator)]?.Value<string>(), ""),
                Difficulty = TufInput.CapDisplay(token[nameof(Difficulty)]?.Value<string>(), "", 24),
                DifficultyColor = TufInput.NormalizeColor(token[nameof(DifficultyColor)]?.Value<string>()),
                Clears = Math.Max(0, token[nameof(Clears)]?.Value<int>() ?? 0),
                Likes = Math.Max(0, token[nameof(Likes)]?.Value<int>() ?? 0),
                Folder = folder,
                DownloadUrl = token[nameof(DownloadUrl)]?.Value<string>() ?? "",
                VideoLink = TufInput.CapDisplay(token[nameof(VideoLink)]?.Value<string>(), "", 300),
                InstalledAtUtc = token[nameof(InstalledAtUtc)]?.Value<long>() ?? 0
            };
        } catch { return null; }
    }
    public TufLevel ToLevel() {
        Uri? uri = Uri.TryCreate(DownloadUrl, UriKind.Absolute, out Uri? parsed)
            && TufNetworkPolicy.IsAllowedDownloadUri(parsed) ? parsed : null;
        return new TufLevel(Id, Song, Artist, Creator, Difficulty, DifficultyColor, Clears, Likes, uri) {
            VideoLink = VideoLink
        };
    }
}
public sealed class TufInstallIndex : ISettingsFile {
    private readonly List<TufInstallEntry> entries = [];
    public IReadOnlyList<TufInstallEntry> Entries => entries;
    public int Count => entries.Count;
    public TufInstallEntry? Find(int id) => entries.FirstOrDefault(e => e.Id == id);
    public void Record(TufLevel level, string folder) {
        if(level == null || level.Id <= 0 || string.IsNullOrWhiteSpace(folder)) return;
        TufInstallEntry? existing = Find(level.Id);
        long installedAt = existing?.InstalledAtUtc ?? 0;
        if(installedAt <= 0) installedAt = DateTime.UtcNow.Ticks;
        if(existing != null) entries.Remove(existing);
        entries.Insert(0, new TufInstallEntry {
            Id = level.Id,
            Song = level.Song,
            Artist = level.Artist,
            Creator = level.Creator,
            Difficulty = level.Difficulty,
            DifficultyColor = level.DifficultyColor,
            Clears = level.Clears,
            Likes = level.Likes,
            Folder = Path.GetFullPath(folder),
            DownloadUrl = level.DownloadUri?.ToString() ?? "",
            VideoLink = level.VideoLink,
            InstalledAtUtc = installedAt
        });
        Sort();
    }
    public TufInstallEntry Adopt(int id, string folder, long installedAtUtc) {
        TufInstallEntry entry = new() {
            Id = id,
            Song = "",
            Artist = "",
            Creator = "",
            Difficulty = "",
            Folder = Path.GetFullPath(folder),
            InstalledAtUtc = installedAtUtc
        };
        entries.Add(entry);
        Sort();
        return entry;
    }
    public bool Remove(int id) {
        TufInstallEntry? entry = Find(id);
        if(entry == null) return false;
        entries.Remove(entry);
        return true;
    }
    public void SetFolder(int id, string folder) {
        TufInstallEntry? entry = Find(id);
        if(entry != null && !string.IsNullOrWhiteSpace(folder)) entry.Folder = Path.GetFullPath(folder);
    }
    public bool PruneMissing() {
        int removed = entries.RemoveAll(e => {
            try { return !Directory.Exists(e.Folder); } catch { return true; }
        });
        return removed > 0;
    }
    private void Sort() => entries.Sort((a, b) => b.InstalledAtUtc.CompareTo(a.InstalledAtUtc));
    public JToken Serialize() => new JObject {
        ["Version"] = 1,
        ["Entries"] = new JArray(entries.Select(e => e.Serialize()).Cast<object>().ToArray())
    };
    public void Deserialize(JToken token) {
        entries.Clear();
        if(token["Entries"] is not JArray array) return;
        HashSet<int> seen = [];
        foreach(JToken item in array) {
            TufInstallEntry? entry = TufInstallEntry.Deserialize(item);
            if(entry != null && seen.Add(entry.Id)) entries.Add(entry);
        }
        Sort();
    }
}

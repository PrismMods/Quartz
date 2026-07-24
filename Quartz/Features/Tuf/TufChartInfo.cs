#nullable enable
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Quartz.Features.Tuf;
public sealed class TufChartInfo {
    private const long MaxScanBytes = 64L * 1024 * 1024;
    public string Song { get; }
    public string Artist { get; }
    public string Creator { get; }
    public bool IsEmpty => Song.Length == 0 && Artist.Length == 0 && Creator.Length == 0;
    private TufChartInfo(string song, string artist, string creator) {
        Song = song;
        Artist = artist;
        Creator = creator;
    }
    public static TufChartInfo? Read(string? chartPath) {
        if(string.IsNullOrWhiteSpace(chartPath)) return null;
        try {
            if(!File.Exists(chartPath)) return null;
            using FileStream file = new(chartPath!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader text = new(file, Encoding.UTF8, true);
            using JsonTextReader reader = new(text) {
                MaxDepth = 16,
                DateParseHandling = DateParseHandling.None
            };
            while(reader.Read()) {
                if(reader.TokenType != JsonToken.PropertyName) {
                    if(file.Position > MaxScanBytes) return null;
                    continue;
                }
                if(!string.Equals(reader.Value as string, "settings", StringComparison.Ordinal)) {
                    reader.Skip();
                    if(file.Position > MaxScanBytes) return null;
                    continue;
                }
                if(!reader.Read() || reader.TokenType != JsonToken.StartObject) return null;
                JObject settings = JObject.Load(reader);
                TufChartInfo info = new(
                    TufInput.CapDisplay(settings.Value<string>("song"), ""),
                    TufInput.CapDisplay(settings.Value<string>("artist"), ""),
                    TufInput.CapDisplay(settings.Value<string>("author"), ""));
                return info.IsEmpty ? null : info;
            }
        } catch { }
        return null;
    }
}

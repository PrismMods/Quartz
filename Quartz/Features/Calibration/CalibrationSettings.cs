using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.Calibration;
public sealed class CalibrationSettings : ISettingsFile {
    public bool ShowPopupOnDeath = true;
    public bool DetailedDisplay = true;
    public bool FloatOffsetEnabled;
    public Dictionary<string, float> FloatOffsetByDevice = new();
    public float SongPitch = 100f;
    public bool SongUseMinimum;
    public int SongMinimum;
    public int SongRepeat;
    public int MaxTimings = 15;
    public int MaxTimingsPerMap = 5;
    public JToken Serialize() {
        JObject offsets = new();
        foreach(KeyValuePair<string, float> kv in FloatOffsetByDevice) offsets[kv.Key] = kv.Value;
        return new JObject {
            [nameof(ShowPopupOnDeath)] = ShowPopupOnDeath,
            [nameof(DetailedDisplay)] = DetailedDisplay,
            [nameof(FloatOffsetEnabled)] = FloatOffsetEnabled,
            [nameof(FloatOffsetByDevice)] = offsets,
            [nameof(SongPitch)] = SongPitch,
            [nameof(SongUseMinimum)] = SongUseMinimum,
            [nameof(SongMinimum)] = SongMinimum,
            [nameof(SongRepeat)] = SongRepeat,
            [nameof(MaxTimings)] = MaxTimings,
            [nameof(MaxTimingsPerMap)] = MaxTimingsPerMap,
        };
    }
    public void Deserialize(JToken token) {
        ShowPopupOnDeath = IOUtils.Read(token, nameof(ShowPopupOnDeath), ShowPopupOnDeath);
        DetailedDisplay = IOUtils.Read(token, nameof(DetailedDisplay), DetailedDisplay);
        FloatOffsetEnabled = IOUtils.Read(token, nameof(FloatOffsetEnabled), FloatOffsetEnabled);
        SongPitch = IOUtils.Read(token, nameof(SongPitch), SongPitch);
        SongUseMinimum = IOUtils.Read(token, nameof(SongUseMinimum), SongUseMinimum);
        SongMinimum = IOUtils.Read(token, nameof(SongMinimum), SongMinimum);
        SongRepeat = IOUtils.Read(token, nameof(SongRepeat), SongRepeat);
        MaxTimings = IOUtils.Read(token, nameof(MaxTimings), MaxTimings);
        MaxTimingsPerMap = IOUtils.Read(token, nameof(MaxTimingsPerMap), MaxTimingsPerMap);
        FloatOffsetByDevice = new Dictionary<string, float>();
        if(token[nameof(FloatOffsetByDevice)] is JObject offsets) {
            foreach(JProperty prop in offsets.Properties()) {
                try {
                    FloatOffsetByDevice[prop.Name] = prop.Value.Value<float>();
                } catch {
                }
            }
        }
    }
}

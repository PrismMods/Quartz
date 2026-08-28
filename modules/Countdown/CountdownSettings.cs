using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.Countdown;
public sealed class CountdownSettings : ISettingsFile {
    public bool Enabled = true;
    public float MinBpm = 400f;
    public float MaxBpm = 600f;
    public JToken Serialize() {
        return new JObject {
            [nameof(Enabled)] = Enabled,
            [nameof(MinBpm)] = MinBpm,
            [nameof(MaxBpm)] = MaxBpm,
        };
    }
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        MinBpm = IOUtils.Read(token, nameof(MinBpm), MinBpm);
        MaxBpm = IOUtils.Read(token, nameof(MaxBpm), MaxBpm);
        if(MaxBpm < MinBpm) MaxBpm = MinBpm;
    }
}

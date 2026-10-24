using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.KeyLimiter;
public sealed class ChartKeyLimiterSettings : ISettingsFile {
    public bool Enabled = true;
    public JToken Serialize() => new JObject {
        [nameof(Enabled)] = Enabled,
    };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
    }
}

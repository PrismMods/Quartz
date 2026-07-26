using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.ChatterBlocker;
public sealed class ChatterBlockerSettings : ISettingsFile {
    public bool Enabled = true;
    public float ThresholdMs = 35f;
    public JToken Serialize() => new JObject {
        [nameof(Enabled)] = Enabled,
        [nameof(ThresholdMs)] = ThresholdMs,
    };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        ThresholdMs = IOUtils.Read(token, nameof(ThresholdMs), ThresholdMs);
    }
}

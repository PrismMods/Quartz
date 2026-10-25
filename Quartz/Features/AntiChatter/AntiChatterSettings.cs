using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.AntiChatter;
public sealed class AntiChatterSettings : ISettingsFile {
    public bool Enabled = true;
    public float ThresholdMs = 35f;
    public bool HoldBounceProtection = true;
    public JToken Serialize() => new JObject {
        [nameof(Enabled)] = Enabled,
        [nameof(ThresholdMs)] = ThresholdMs,
        [nameof(HoldBounceProtection)] = HoldBounceProtection,
    };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        ThresholdMs = IOUtils.Read(token, nameof(ThresholdMs), ThresholdMs);
        HoldBounceProtection = IOUtils.Read(token, nameof(HoldBounceProtection), HoldBounceProtection);
    }
}

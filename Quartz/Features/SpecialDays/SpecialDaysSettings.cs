using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.SpecialDays;
public enum SpecialDayMode {
    Auto,
    On,
    Off,
}
public sealed class SpecialDaysSettings : ISettingsFile {
    public SpecialDayMode Halloween = SpecialDayMode.Auto;
    public SpecialDayMode AprilFools = SpecialDayMode.Auto;
    public JToken Serialize() => new JObject {
        [nameof(Halloween)] = (int)Halloween,
        [nameof(AprilFools)] = (int)AprilFools,
    };
    public void Deserialize(JToken token) {
        Halloween = Read(token, nameof(Halloween), Halloween);
        AprilFools = Read(token, nameof(AprilFools), AprilFools);
    }
    private static SpecialDayMode Read(JToken token, string key, SpecialDayMode fallback) {
        int v = IOUtils.Read(token, key, (int)fallback);
        return v is >= 0 and <= 2 ? (SpecialDayMode)v : fallback;
    }
}

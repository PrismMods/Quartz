using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.Accuracy;
public sealed class AccuracySettings : ISettingsFile {
    public bool Enabled = true;
    public bool JeaEnabled = true;
    public bool NeaEnabled = true;
    public bool ShowResultsLine = true;
    public bool ShowHitText = true;
    public bool ShowDeathMarkers = true;
    public JToken Serialize() => new JObject {
        [nameof(Enabled)] = Enabled,
        [nameof(JeaEnabled)] = JeaEnabled,
        [nameof(NeaEnabled)] = NeaEnabled,
        [nameof(ShowResultsLine)] = ShowResultsLine,
        [nameof(ShowHitText)] = ShowHitText,
        [nameof(ShowDeathMarkers)] = ShowDeathMarkers,
    };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        JeaEnabled = IOUtils.Read(token, nameof(JeaEnabled), JeaEnabled);
        NeaEnabled = IOUtils.Read(token, nameof(NeaEnabled), NeaEnabled);
        ShowResultsLine = IOUtils.Read(token, nameof(ShowResultsLine), ShowResultsLine);
        ShowHitText = IOUtils.Read(token, nameof(ShowHitText), ShowHitText);
        ShowDeathMarkers = IOUtils.Read(token, nameof(ShowDeathMarkers), ShowDeathMarkers);
    }
}

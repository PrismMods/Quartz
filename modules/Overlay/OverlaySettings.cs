using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Overlay;
public sealed class OverlaySettings : ISettingsFile {
    public bool Enabled = true;
    public JToken Serialize() => new JObject {
        [nameof(Enabled)] = Enabled,
    };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), true);
    }
}

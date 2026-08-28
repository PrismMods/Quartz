using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.Features.TileArc;
public sealed class TileArcSettings : ISettingsFile {
    public bool Enabled = false;
    public float Intensity = 0.9f;
    public JToken Serialize() => new JObject {
        [nameof(Enabled)] = Enabled,
        [nameof(Intensity)] = Intensity,
    };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        Intensity = Mathf.Clamp01(IOUtils.Read(token, nameof(Intensity), Intensity));
    }
}

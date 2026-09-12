using GTweens.Easings;
using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.Overlay;
public sealed class OverlaySettings : ISettingsFile {
    public bool Enabled = true;
    public bool PopEnabled;
    public bool PopExit = true;
    public OverlayPopEdge PopEdge = OverlayPopEdge.Nearest;
    public Easing PopEase = Easing.OutCubic;
    public float PopSeconds = 0.5f;
    public JToken Serialize() => new JObject {
        [nameof(Enabled)] = Enabled,
        [nameof(PopEnabled)] = PopEnabled,
        [nameof(PopExit)] = PopExit,
        [nameof(PopEdge)] = PopEdge.ToString(),
        [nameof(PopEase)] = PopEase.ToString(),
        [nameof(PopSeconds)] = PopSeconds,
    };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), true);
        PopEnabled = IOUtils.Read(token, nameof(PopEnabled), false);
        PopExit = IOUtils.Read(token, nameof(PopExit), true);
        if(Enum.TryParse(IOUtils.Read(token, nameof(PopEdge), PopEdge.ToString()), true, out OverlayPopEdge edge))
            PopEdge = edge;
        if(Enum.TryParse(IOUtils.Read(token, nameof(PopEase), PopEase.ToString()), true, out Easing ease))
            PopEase = ease;
        PopSeconds = Mathf.Clamp(
            IOUtils.Read(token, nameof(PopSeconds), 0.5f),
            OverlayMotion.MinSeconds,
            OverlayMotion.MaxSeconds
        );
    }
}

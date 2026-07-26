using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.Features.OttoIcon;
public sealed class OttoIconSettings : ISettingsFile {
    public bool Enabled = true;
    public float R = 1f;
    public float G = 0f;
    public float B = 0f;
    public float A = 1f;
    public bool UseHighBpmColor = false;
    public float HighBpmR = 1f;
    public float HighBpmG = 0f;
    public float HighBpmB = 0f;
    public float HighBpmA = 1f;
    public float OffsetX = -10f;
    public float OffsetY = 5f;
    public Color GetColor() => IOUtils.Rgba(R, G, B, A);
    public void SetColor(Color c) => IOUtils.SetRgba(c, ref R, ref G, ref B, ref A);
    public Color GetHighBpmColor() => IOUtils.Rgba(HighBpmR, HighBpmG, HighBpmB, HighBpmA);
    public void SetHighBpmColor(Color c) =>
        IOUtils.SetRgba(c, ref HighBpmR, ref HighBpmG, ref HighBpmB, ref HighBpmA);
    public JToken Serialize() => new JObject {
            [nameof(Enabled)] = Enabled,
            [nameof(R)] = R,
            [nameof(G)] = G,
            [nameof(B)] = B,
            [nameof(A)] = A,
            [nameof(UseHighBpmColor)] = UseHighBpmColor,
            [nameof(HighBpmR)] = HighBpmR,
            [nameof(HighBpmG)] = HighBpmG,
            [nameof(HighBpmB)] = HighBpmB,
            [nameof(HighBpmA)] = HighBpmA,
            [nameof(OffsetX)] = OffsetX,
            [nameof(OffsetY)] = OffsetY,
    };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        IOUtils.ReadRgba(token, "", ref R, ref G, ref B, ref A);
        UseHighBpmColor = IOUtils.Read(token, nameof(UseHighBpmColor), UseHighBpmColor);
        IOUtils.ReadRgba(token, "HighBpm", ref HighBpmR, ref HighBpmG, ref HighBpmB, ref HighBpmA);
        OffsetX = IOUtils.Read(token, nameof(OffsetX), OffsetX);
        OffsetY = IOUtils.Read(token, nameof(OffsetY), OffsetY);
    }
}

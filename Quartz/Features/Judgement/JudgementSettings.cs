using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.Features.Judgement;
public sealed class JudgementSettings : ISettingsFile {
    public bool Enabled = true;
    public bool CompactRow = true;
    public bool ShowXPerfect = true;
    public float OffsetX = 0f;
    public float OffsetY = -5f;
    public float Size = 0.9f;
    public float Spacing = 5f;
    public bool TextShadowEnabled = true;
    public float TextShadowX = 1.5f;
    public float TextShadowY = -1.5f;
    public float TextShadowSoftness = 0f;
    public float TextShadowR = 0f, TextShadowG = 0f, TextShadowB = 0f, TextShadowA = 0.5019608f;
    public Color GetTextShadowColor() => IOUtils.Rgba(TextShadowR, TextShadowG, TextShadowB, TextShadowA);
    public void SetTextShadowColor(Color c) =>
        IOUtils.SetRgba(c, ref TextShadowR, ref TextShadowG, ref TextShadowB, ref TextShadowA);
    public JToken Serialize() => new JObject {
            [nameof(Enabled)] = Enabled,
            [nameof(CompactRow)] = CompactRow,
            [nameof(ShowXPerfect)] = ShowXPerfect,
            [nameof(OffsetX)] = OffsetX,
            [nameof(OffsetY)] = OffsetY,
            [nameof(Size)] = Size,
            [nameof(Spacing)] = Spacing,
            [nameof(TextShadowEnabled)] = TextShadowEnabled,
            [nameof(TextShadowX)] = TextShadowX,
            [nameof(TextShadowY)] = TextShadowY,
            [nameof(TextShadowSoftness)] = TextShadowSoftness,
            [nameof(TextShadowR)] = TextShadowR,
            [nameof(TextShadowG)] = TextShadowG,
            [nameof(TextShadowB)] = TextShadowB,
            [nameof(TextShadowA)] = TextShadowA,
        };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        CompactRow = IOUtils.Read(token, nameof(CompactRow), CompactRow);
        ShowXPerfect = IOUtils.Read(token, nameof(ShowXPerfect), ShowXPerfect);
        OffsetX = IOUtils.Read(token, nameof(OffsetX), OffsetX);
        OffsetY = IOUtils.Read(token, nameof(OffsetY), OffsetY);
        Size = IOUtils.Read(token, nameof(Size), Size);
        Spacing = IOUtils.Read(token, nameof(Spacing), Spacing);
        TextShadowEnabled = IOUtils.Read(token, nameof(TextShadowEnabled), TextShadowEnabled);
        TextShadowX = IOUtils.Read(token, nameof(TextShadowX), TextShadowX);
        TextShadowY = IOUtils.Read(token, nameof(TextShadowY), TextShadowY);
        TextShadowSoftness = IOUtils.Read(token, nameof(TextShadowSoftness), TextShadowSoftness);
        IOUtils.ReadRgba(token, "TextShadow", ref TextShadowR, ref TextShadowG, ref TextShadowB, ref TextShadowA);
    }
}

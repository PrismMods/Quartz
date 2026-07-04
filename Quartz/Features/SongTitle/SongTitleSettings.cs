using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;

namespace Quartz.Features.SongTitle;

// Persisted config for the in-game Song Title overlay (UserData/Quartz/SongTitle.json).
// When enabled it hides the game's own level-title HUD and draws a customizable
// replacement built from the {artist} and {title} tags in Format.
public sealed class SongTitleSettings : ISettingsFile {
    public bool Enabled = true;

    // Layout template. {artist} and {title} are replaced with the level's author
    // and song. Default reproduces the game's "artist - title".
    public string Format = "{artist} - {title}";

    public float FontSize = 40f;
    public float MasterSize = 1f;

    // Top-center anchored; OffsetY is measured down from the top edge (negative
    // = lower). Defaults sit roughly where the game's own title is.
    public float OffsetX = 0f;
    public float OffsetY = -55f;

    public float ColorR = 1f, ColorG = 1f, ColorB = 1f, ColorA = 1f;

    public bool ShadowEnabled = true;
    public float ShadowX = 2f, ShadowY = -2f, ShadowSoftness = 0f;
    public float ShadowR = 0f, ShadowG = 0f, ShadowB = 0f, ShadowA = 0.3959352f;

    public Color GetColor() => IOUtils.Rgba(ColorR, ColorG, ColorB, ColorA);
    public void SetColor(Color c) => IOUtils.SetRgba(c, ref ColorR, ref ColorG, ref ColorB, ref ColorA);

    public Color GetShadowColor() => IOUtils.Rgba(ShadowR, ShadowG, ShadowB, ShadowA);
    public void SetShadowColor(Color c) => IOUtils.SetRgba(c, ref ShadowR, ref ShadowG, ref ShadowB, ref ShadowA);

    public JToken Serialize() => new JObject {
        [nameof(Enabled)] = Enabled,
        [nameof(Format)] = Format,
        [nameof(FontSize)] = FontSize,
        [nameof(MasterSize)] = MasterSize,
        [nameof(OffsetX)] = OffsetX,
        [nameof(OffsetY)] = OffsetY,
        [nameof(ColorR)] = ColorR,
        [nameof(ColorG)] = ColorG,
        [nameof(ColorB)] = ColorB,
        [nameof(ColorA)] = ColorA,
        [nameof(ShadowEnabled)] = ShadowEnabled,
        [nameof(ShadowX)] = ShadowX,
        [nameof(ShadowY)] = ShadowY,
        [nameof(ShadowSoftness)] = ShadowSoftness,
        [nameof(ShadowR)] = ShadowR,
        [nameof(ShadowG)] = ShadowG,
        [nameof(ShadowB)] = ShadowB,
        [nameof(ShadowA)] = ShadowA,
    };

    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        Format = IOUtils.Read(token, nameof(Format), Format);
        FontSize = IOUtils.Read(token, nameof(FontSize), FontSize);
        MasterSize = IOUtils.Read(token, nameof(MasterSize), MasterSize);
        OffsetX = IOUtils.Read(token, nameof(OffsetX), OffsetX);
        OffsetY = IOUtils.Read(token, nameof(OffsetY), OffsetY);
        IOUtils.ReadRgba(token, "Color", ref ColorR, ref ColorG, ref ColorB, ref ColorA);
        ShadowEnabled = IOUtils.Read(token, nameof(ShadowEnabled), ShadowEnabled);
        ShadowX = IOUtils.Read(token, nameof(ShadowX), ShadowX);
        ShadowY = IOUtils.Read(token, nameof(ShadowY), ShadowY);
        ShadowSoftness = IOUtils.Read(token, nameof(ShadowSoftness), ShadowSoftness);
        IOUtils.ReadRgba(token, "Shadow", ref ShadowR, ref ShadowG, ref ShadowB, ref ShadowA);
    }
}

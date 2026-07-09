using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.Features.Panels;
public sealed class StatEntry {
    public string Id = "";
    public bool Enabled = true;
    public bool ShowLabel = true;
    public string Text = "";
    public StatColor Color;
    public StatEntry() { }
    public StatEntry(string id) => Id = id;
    public StatColor EnsureColor() => Color ??= StatColor.DefaultFor(Id);
    public JToken Serialize() {
        JObject obj = new() {
            [nameof(Id)] = Id,
            [nameof(Enabled)] = Enabled,
            [nameof(ShowLabel)] = ShowLabel,
        };
        if(!string.IsNullOrEmpty(Text)) obj[nameof(Text)] = Text;
        if(Color != null) obj[nameof(Color)] = Color.Serialize();
        return obj;
    }
    public static StatEntry Deserialize(JToken token) {
        if(token is JValue) return new StatEntry(token.ToString());
        StatEntry e = new();
        e.Id = IOUtils.Read(token, nameof(Id), e.Id);
        e.Enabled = IOUtils.Read(token, nameof(Enabled), e.Enabled);
        e.ShowLabel = IOUtils.Read(token, nameof(ShowLabel), e.ShowLabel);
        e.Text = IOUtils.Read(token, nameof(Text), e.Text);
        if(token[nameof(Color)] is JObject color) e.Color = StatColor.Deserialize(color);
        return e;
    }
}
public enum PanelAnchor {
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}
public sealed class PanelConfig {
    public string Name = "Panel";
    public int Anchor = (int)PanelAnchor.TopLeft;
    public float PosX = 24f;
    public float PosY = -24f;
    public static Vector2 DefaultOffset(PanelAnchor anchor) {
        Vector2 a = AnchorVector(anchor);
        return new Vector2(
            a.x == 0f ? 24f : a.x == 1f ? -24f : 0f,
            a.y == 0f ? 24f : a.y == 1f ? -24f : 0f
        );
    }
    public static Vector2 AnchorVector(PanelAnchor anchor) => anchor switch {
        PanelAnchor.TopLeft => new Vector2(0f, 1f),
        PanelAnchor.TopCenter => new Vector2(0.5f, 1f),
        PanelAnchor.TopRight => new Vector2(1f, 1f),
        PanelAnchor.MiddleLeft => new Vector2(0f, 0.5f),
        PanelAnchor.MiddleCenter => new Vector2(0.5f, 0.5f),
        PanelAnchor.MiddleRight => new Vector2(1f, 0.5f),
        PanelAnchor.BottomLeft => new Vector2(0f, 0f),
        PanelAnchor.BottomCenter => new Vector2(0.5f, 0f),
        PanelAnchor.BottomRight => new Vector2(1f, 0f),
        _ => new Vector2(0f, 1f),
    };
    public List<StatEntry> Stats = [];
    public string Prefix = "";
    public int Decimals = 2;
    public float FontSize = 22f;
    public string LabelSeparator = "|";
    public float LineSpacing = 0f;
    public bool BackgroundEnabled = true;
    public float BgR = 0.165f;
    public float BgG = 0.161f;
    public float BgB = 0.196f;
    public float BgA = 1f;
    public bool LocalizeStatLabels = false;
    public float TextR = 1f;
    public float TextG = 1f;
    public float TextB = 1f;
    public float TextA = 1f;
    public bool TextShadowEnabled = true;
    public float TextShadowX = 2f;
    public float TextShadowY = -2f;
    public float TextShadowSoftness = 0f;
    public float TextShadowR = 0f;
    public float TextShadowG = 0f;
    public float TextShadowB = 0f;
    public float TextShadowA = 0.75f;
    public Color GetTextColor() => IOUtils.Rgba(TextR, TextG, TextB, TextA);
    public void SetTextColor(Color c) => IOUtils.SetRgba(c, ref TextR, ref TextG, ref TextB, ref TextA);
    public Color GetBackgroundColor() => IOUtils.Rgba(BgR, BgG, BgB, BgA);
    public void SetBackgroundColor(Color c) => IOUtils.SetRgba(c, ref BgR, ref BgG, ref BgB, ref BgA);
    public Color GetTextShadowColor() => IOUtils.Rgba(TextShadowR, TextShadowG, TextShadowB, TextShadowA);
    public void SetTextShadowColor(Color c) =>
        IOUtils.SetRgba(c, ref TextShadowR, ref TextShadowG, ref TextShadowB, ref TextShadowA);
    public JToken Serialize() {
        JArray stats = [];
        foreach(StatEntry e in Stats) stats.Add(e.Serialize());
        return new JObject {
            [nameof(Name)] = Name,
            [nameof(Anchor)] = Anchor,
            [nameof(PosX)] = PosX,
            [nameof(PosY)] = PosY,
            [nameof(Stats)] = stats,
            [nameof(Prefix)] = Prefix,
            [nameof(Decimals)] = Decimals,
            [nameof(FontSize)] = FontSize,
            [nameof(LabelSeparator)] = LabelSeparator,
            [nameof(LineSpacing)] = LineSpacing,
            [nameof(BackgroundEnabled)] = BackgroundEnabled,
            [nameof(BgR)] = BgR,
            [nameof(BgG)] = BgG,
            [nameof(BgB)] = BgB,
            [nameof(BgA)] = BgA,
            [nameof(LocalizeStatLabels)] = LocalizeStatLabels,
            [nameof(TextR)] = TextR,
            [nameof(TextG)] = TextG,
            [nameof(TextB)] = TextB,
            [nameof(TextA)] = TextA,
            [nameof(TextShadowEnabled)] = TextShadowEnabled,
            [nameof(TextShadowX)] = TextShadowX,
            [nameof(TextShadowY)] = TextShadowY,
            [nameof(TextShadowSoftness)] = TextShadowSoftness,
            [nameof(TextShadowR)] = TextShadowR,
            [nameof(TextShadowG)] = TextShadowG,
            [nameof(TextShadowB)] = TextShadowB,
            [nameof(TextShadowA)] = TextShadowA,
        };
    }
    public static PanelConfig Deserialize(JToken token) {
        PanelConfig p = new();
        if(token == null) return p;
        p.Name = IOUtils.Read(token, nameof(Name), p.Name);
        p.Anchor = IOUtils.Read(token, nameof(Anchor), p.Anchor);
        p.PosX = IOUtils.Read(token, nameof(PosX), p.PosX);
        p.PosY = IOUtils.Read(token, nameof(PosY), p.PosY);
        if(token[nameof(Stats)] is JArray arr) {
            p.Stats = [];
            foreach(JToken t in arr) {
                StatEntry e = StatEntry.Deserialize(t);
                if(!string.IsNullOrEmpty(e.Id)) p.Stats.Add(e);
            }
        }
        p.Prefix = IOUtils.Read(token, nameof(Prefix), p.Prefix);
        p.Decimals = IOUtils.Read(token, nameof(Decimals), p.Decimals);
        p.FontSize = IOUtils.Read(token, nameof(FontSize), p.FontSize);
        p.LabelSeparator = IOUtils.Read(token, nameof(LabelSeparator), p.LabelSeparator);
        p.LineSpacing = IOUtils.Read(token, nameof(LineSpacing), p.LineSpacing);
        p.BackgroundEnabled = IOUtils.Read(token, nameof(BackgroundEnabled), p.BackgroundEnabled);
        IOUtils.ReadRgba(token, "Bg", ref p.BgR, ref p.BgG, ref p.BgB, ref p.BgA);
        p.LocalizeStatLabels = IOUtils.Read(token, nameof(LocalizeStatLabels), p.LocalizeStatLabels);
        IOUtils.ReadRgba(token, "Text", ref p.TextR, ref p.TextG, ref p.TextB, ref p.TextA);
        p.TextShadowEnabled = IOUtils.Read(token, nameof(TextShadowEnabled), p.TextShadowEnabled);
        p.TextShadowX = IOUtils.Read(token, nameof(TextShadowX), p.TextShadowX);
        p.TextShadowY = IOUtils.Read(token, nameof(TextShadowY), p.TextShadowY);
        p.TextShadowSoftness = IOUtils.Read(token, nameof(TextShadowSoftness), p.TextShadowSoftness);
        IOUtils.ReadRgba(token, "TextShadow", ref p.TextShadowR, ref p.TextShadowG, ref p.TextShadowB, ref p.TextShadowA);
        return p;
    }
}
public sealed class PanelsSettings : ISettingsFile {
    public bool Enabled = true;
    public List<PanelConfig> Panels = [
        new PanelConfig {
            Name = "left",
            Anchor = (int)PanelAnchor.TopLeft,
            PosX = 22.7f,
            PosY = -19.5f,
            Stats = [new("progress"), new("best"), new("xaccuracy"), new("maxaccuracy"), new("fps")],
            LabelSeparator = "|",
            BackgroundEnabled = false,
            TextShadowX = 1.5f,
            TextShadowY = -1.5f,
            TextShadowA = 0.5f,
        },
        new PanelConfig {
            Name = "right",
            Anchor = (int)PanelAnchor.TopRight,
            PosX = -19.2f,
            PosY = -28.5f,
            Stats = [new("tbpm"), new("cbpm"), new("kps")],
            LabelSeparator = "|",
            BackgroundEnabled = false,
            TextShadowX = 1.5f,
            TextShadowY = -1.5f,
            TextShadowA = 0.5f,
        },
        new PanelConfig {
            Name = "attempts",
            Anchor = (int)PanelAnchor.BottomRight,
            PosX = 1.5f,
            PosY = 63.8f,
            Stats = [new("attempt"), new("totalattempts")],
            LabelSeparator = "|",
            BackgroundEnabled = false,
            TextShadowX = 1.5f,
            TextShadowY = -1.5f,
        },
    ];
    public JToken Serialize() {
        JArray panels = [];
        foreach(PanelConfig p in Panels) panels.Add(p.Serialize());
        return new JObject {
            [nameof(Enabled)] = Enabled,
            [nameof(Panels)] = panels,
        };
    }
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        if(token?[nameof(Panels)] is JArray arr) {
            Panels = [];
            foreach(JToken t in arr) Panels.Add(PanelConfig.Deserialize(t));
        }
    }
}

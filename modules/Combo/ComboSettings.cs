using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.Features.Combo;
public sealed class ComboSettings : ISettingsFile {
    public bool Enabled = true;
    public bool CountAuto = true;
    public bool XPerfectComboEnabled = false;
    public float FontSize = 56f;
    public float MasterSize = 1f;
    public float CaptionScale = 0.35f;
    public float OffsetX = 0f;
    public float OffsetY = 58.8050537f;
    public bool ShowCaption = true;
    public string CaptionText = "Combo";
    public float CaptionOffsetY = -40f;
    public bool CaptionShadowEnabled = true;
    public float CaptionShadowX = 1.5f;
    public float CaptionShadowY = -1.5f;
    public float CaptionShadowSoftness = 0f;
    public float CaptionShadowR = 0f, CaptionShadowG = 0f, CaptionShadowB = 0f, CaptionShadowA = 0.5019608f;
    public float CountThickness = 0f;
    public bool CountShadowEnabled = true;
    public float CountShadowX = 1.5f;
    public float CountShadowY = -1.5f;
    public float CountShadowSoftness = 0f;
    public float CountShadowR = 0f, CountShadowG = 0f, CountShadowB = 0f, CountShadowA = 0.5019608f;
    public int ColorMax = 2000;
    public float ColorLowR = 1f, ColorLowG = 1f, ColorLowB = 1f, ColorLowA = 1f;
    public float ColorHighR = 1f, ColorHighG = 0.22f, ColorHighB = 0.22f, ColorHighA = 1f;
    public bool SolidColor = false;
    public bool PerfectColorEnabled = false;
    public float PerfectR = 0.886f, PerfectG = 0.404f, PerfectB = 0.427f, PerfectA = 1f;
    public bool NoPopAnim = false;
    public float CountPulseScale = 0.149999991f;
    public float PulseDuration = 0.099999994f;
    public float LabelPulseOffsetY = 7f;
    public Color GetColorLow() => IOUtils.Rgba(ColorLowR, ColorLowG, ColorLowB, ColorLowA);
    public void SetColorLow(Color c) => IOUtils.SetRgba(c, ref ColorLowR, ref ColorLowG, ref ColorLowB, ref ColorLowA);
    public Color GetColorHigh() => IOUtils.Rgba(ColorHighR, ColorHighG, ColorHighB, ColorHighA);
    public void SetColorHigh(Color c) => IOUtils.SetRgba(c, ref ColorHighR, ref ColorHighG, ref ColorHighB, ref ColorHighA);
    public Color GetPerfectColor() => IOUtils.Rgba(PerfectR, PerfectG, PerfectB, PerfectA);
    public void SetPerfectColor(Color c) => IOUtils.SetRgba(c, ref PerfectR, ref PerfectG, ref PerfectB, ref PerfectA);
    public Color GetCaptionShadowColor() => IOUtils.Rgba(CaptionShadowR, CaptionShadowG, CaptionShadowB, CaptionShadowA);
    public void SetCaptionShadowColor(Color c) =>
        IOUtils.SetRgba(c, ref CaptionShadowR, ref CaptionShadowG, ref CaptionShadowB, ref CaptionShadowA);
    public Color GetCountShadowColor() => IOUtils.Rgba(CountShadowR, CountShadowG, CountShadowB, CountShadowA);
    public void SetCountShadowColor(Color c) =>
        IOUtils.SetRgba(c, ref CountShadowR, ref CountShadowG, ref CountShadowB, ref CountShadowA);
    public Color GetComboColor(int combo) {
        if(PerfectColorEnabled && ColorMax > 0 && combo >= ColorMax) return GetPerfectColor();
        if(SolidColor) return GetColorLow();
        return Color.Lerp(GetColorLow(), GetColorHigh(), ColorMax <= 0 ? 0f : Mathf.Clamp01((float)combo / ColorMax));
    }
    public JToken Serialize() {
        return new JObject {
            [nameof(Enabled)] = Enabled,
            [nameof(CountAuto)] = CountAuto,
            [nameof(XPerfectComboEnabled)] = XPerfectComboEnabled,
            [nameof(FontSize)] = FontSize,
            [nameof(MasterSize)] = MasterSize,
            [nameof(CaptionScale)] = CaptionScale,
            [nameof(OffsetX)] = OffsetX,
            [nameof(OffsetY)] = OffsetY,
            [nameof(ShowCaption)] = ShowCaption,
            [nameof(CaptionText)] = CaptionText,
            [nameof(CaptionOffsetY)] = CaptionOffsetY,
            [nameof(CaptionShadowEnabled)] = CaptionShadowEnabled,
            [nameof(CaptionShadowX)] = CaptionShadowX,
            [nameof(CaptionShadowY)] = CaptionShadowY,
            [nameof(CaptionShadowSoftness)] = CaptionShadowSoftness,
            [nameof(CaptionShadowR)] = CaptionShadowR,
            [nameof(CaptionShadowG)] = CaptionShadowG,
            [nameof(CaptionShadowB)] = CaptionShadowB,
            [nameof(CaptionShadowA)] = CaptionShadowA,
            [nameof(CountThickness)] = CountThickness,
            [nameof(CountShadowEnabled)] = CountShadowEnabled,
            [nameof(CountShadowX)] = CountShadowX,
            [nameof(CountShadowY)] = CountShadowY,
            [nameof(CountShadowSoftness)] = CountShadowSoftness,
            [nameof(CountShadowR)] = CountShadowR,
            [nameof(CountShadowG)] = CountShadowG,
            [nameof(CountShadowB)] = CountShadowB,
            [nameof(CountShadowA)] = CountShadowA,
            [nameof(ColorMax)] = ColorMax,
            [nameof(ColorLowR)] = ColorLowR,
            [nameof(ColorLowG)] = ColorLowG,
            [nameof(ColorLowB)] = ColorLowB,
            [nameof(ColorLowA)] = ColorLowA,
            [nameof(ColorHighR)] = ColorHighR,
            [nameof(ColorHighG)] = ColorHighG,
            [nameof(ColorHighB)] = ColorHighB,
            [nameof(ColorHighA)] = ColorHighA,
            [nameof(SolidColor)] = SolidColor,
            [nameof(PerfectColorEnabled)] = PerfectColorEnabled,
            [nameof(PerfectR)] = PerfectR,
            [nameof(PerfectG)] = PerfectG,
            [nameof(PerfectB)] = PerfectB,
            [nameof(PerfectA)] = PerfectA,
            [nameof(NoPopAnim)] = NoPopAnim,
            [nameof(CountPulseScale)] = CountPulseScale,
            [nameof(PulseDuration)] = PulseDuration,
            [nameof(LabelPulseOffsetY)] = LabelPulseOffsetY,
        };
    }
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        CountAuto = IOUtils.Read(token, nameof(CountAuto), CountAuto);
        XPerfectComboEnabled = IOUtils.Read(token, nameof(XPerfectComboEnabled), XPerfectComboEnabled);
        FontSize = IOUtils.Read(token, nameof(FontSize), FontSize);
        MasterSize = IOUtils.Read(token, nameof(MasterSize), MasterSize);
        CaptionScale = IOUtils.Read(token, nameof(CaptionScale), CaptionScale);
        OffsetX = IOUtils.Read(token, nameof(OffsetX), OffsetX);
        OffsetY = IOUtils.Read(token, nameof(OffsetY), OffsetY);
        ShowCaption = IOUtils.Read(token, nameof(ShowCaption), ShowCaption);
        CaptionText = IOUtils.Read(token, nameof(CaptionText), CaptionText);
        CaptionOffsetY = IOUtils.Read(token, nameof(CaptionOffsetY), CaptionOffsetY);
        bool hasCaptionShadowEnabled = token?[nameof(CaptionShadowEnabled)] != null;
        bool hasCaptionShadowOffset = token?[nameof(CaptionShadowX)] != null || token?[nameof(CaptionShadowY)] != null;
        CaptionShadowEnabled = IOUtils.Read(token, nameof(CaptionShadowEnabled), CaptionShadowEnabled);
        CaptionShadowX = IOUtils.Read(token, nameof(CaptionShadowX), CaptionShadowX);
        CaptionShadowY = IOUtils.Read(token, nameof(CaptionShadowY), CaptionShadowY);
        CaptionShadowSoftness = IOUtils.Read(token, nameof(CaptionShadowSoftness), CaptionShadowSoftness);
        IOUtils.ReadRgba(token, "CaptionShadow", ref CaptionShadowR, ref CaptionShadowG, ref CaptionShadowB, ref CaptionShadowA);
        CountThickness = IOUtils.Read(token, nameof(CountThickness), CountThickness);
        bool hasCountShadowEnabled = token?[nameof(CountShadowEnabled)] != null;
        bool hasCountShadowOffset = token?[nameof(CountShadowX)] != null || token?[nameof(CountShadowY)] != null;
        CountShadowEnabled = IOUtils.Read(token, nameof(CountShadowEnabled), CountShadowEnabled);
        CountShadowX = IOUtils.Read(token, nameof(CountShadowX), CountShadowX);
        CountShadowY = IOUtils.Read(token, nameof(CountShadowY), CountShadowY);
        CountShadowSoftness = IOUtils.Read(token, nameof(CountShadowSoftness), CountShadowSoftness);
        IOUtils.ReadRgba(token, "CountShadow", ref CountShadowR, ref CountShadowG, ref CountShadowB, ref CountShadowA);
        ColorMax = IOUtils.Read(token, nameof(ColorMax), ColorMax);
        IOUtils.ReadRgba(token, "ColorLow", ref ColorLowR, ref ColorLowG, ref ColorLowB, ref ColorLowA);
        IOUtils.ReadRgba(token, "ColorHigh", ref ColorHighR, ref ColorHighG, ref ColorHighB, ref ColorHighA);
        SolidColor = IOUtils.Read(token, nameof(SolidColor), SolidColor);
        PerfectColorEnabled = IOUtils.Read(token, nameof(PerfectColorEnabled), PerfectColorEnabled);
        IOUtils.ReadRgba(token, "Perfect", ref PerfectR, ref PerfectG, ref PerfectB, ref PerfectA);
        NoPopAnim = IOUtils.Read(token, nameof(NoPopAnim), NoPopAnim);
        CountPulseScale = IOUtils.Read(token, nameof(CountPulseScale), CountPulseScale);
        PulseDuration = IOUtils.Read(token, nameof(PulseDuration), PulseDuration);
        LabelPulseOffsetY = IOUtils.Read(token, nameof(LabelPulseOffsetY), LabelPulseOffsetY);
        CountAuto = IOUtils.Read(token, "ComboCountAuto", CountAuto);
        Enabled = IOUtils.Read(token, "ShowCombo", Enabled);
        if(!hasCaptionShadowEnabled) {
            CaptionShadowEnabled = true;
            if(hasCaptionShadowOffset &&
                Mathf.Abs(CaptionShadowX) <= 0.001f &&
                Mathf.Abs(CaptionShadowY) <= 0.001f
            ) {
                CaptionShadowX = 2f;
                CaptionShadowY = -2f;
            }
        }
        if(!hasCountShadowEnabled) {
            CountShadowEnabled = true;
            if(hasCountShadowOffset &&
                Mathf.Abs(CountShadowX) <= 0.001f &&
                Mathf.Abs(CountShadowY) <= 0.001f
            ) {
                CountShadowX = 2f;
                CountShadowY = -2f;
            }
        }
    }
}

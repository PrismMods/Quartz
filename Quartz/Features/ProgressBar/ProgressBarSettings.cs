using Newtonsoft.Json.Linq;
using Quartz.Features.Panels;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.Features.ProgressBar;
public sealed class ProgressBarSettings : ISettingsFile {
    public bool Enabled = true;
    public float Width = 800f;
    public float Height = 8f;
    public float OffsetX = 0f;
    public float TopOffset = 10f;
    public float Rounding = 1f;
    public float OutlineThickness = 1.75f;
    public bool PrefillStart = false;
    public bool UseMapTime = false;
    public float FillR = 1f, FillG = 0f, FillB = 0f, FillA = 0.96f;
    public float BackR = 0.05f, BackG = 0.05f, BackB = 0.06f, BackA = 0.80f;
    public float OutlineColR = 1f, OutlineColG = 1f, OutlineColB = 1f, OutlineColA = 1f;
    public StatColor FillGradient = StatColor.DefaultFor("progress");
    public Color GetFillColorForProgress(float progress) =>
        FillGradient is { Enabled: true } ? FillGradient.Evaluate(progress) : GetFillColor();
    public Color GetFillColor() => IOUtils.Rgba(FillR, FillG, FillB, FillA);
    public void SetFillColor(Color c) => IOUtils.SetRgba(c, ref FillR, ref FillG, ref FillB, ref FillA);
    public Color GetBackColor() => IOUtils.Rgba(BackR, BackG, BackB, BackA);
    public void SetBackColor(Color c) => IOUtils.SetRgba(c, ref BackR, ref BackG, ref BackB, ref BackA);
    public Color GetOutlineColor() => IOUtils.Rgba(OutlineColR, OutlineColG, OutlineColB, OutlineColA);
    public void SetOutlineColor(Color c) =>
        IOUtils.SetRgba(c, ref OutlineColR, ref OutlineColG, ref OutlineColB, ref OutlineColA);
    public JToken Serialize() =>
        new JObject {
            [nameof(Enabled)] = Enabled,
            [nameof(Width)] = Width,
            [nameof(Height)] = Height,
            [nameof(OffsetX)] = OffsetX,
            [nameof(TopOffset)] = TopOffset,
            [nameof(Rounding)] = Rounding,
            [nameof(OutlineThickness)] = OutlineThickness,
            [nameof(PrefillStart)] = PrefillStart,
            [nameof(UseMapTime)] = UseMapTime,
            [nameof(FillR)] = FillR,
            [nameof(FillG)] = FillG,
            [nameof(FillB)] = FillB,
            [nameof(FillA)] = FillA,
            [nameof(BackR)] = BackR,
            [nameof(BackG)] = BackG,
            [nameof(BackB)] = BackB,
            [nameof(BackA)] = BackA,
            [nameof(OutlineColR)] = OutlineColR,
            [nameof(OutlineColG)] = OutlineColG,
            [nameof(OutlineColB)] = OutlineColB,
            [nameof(OutlineColA)] = OutlineColA,
            [nameof(FillGradient)] = FillGradient?.Serialize(),
        };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        Width = IOUtils.Read(token, nameof(Width), Width);
        Height = IOUtils.Read(token, nameof(Height), Height);
        OffsetX = IOUtils.Read(token, nameof(OffsetX), OffsetX);
        TopOffset = IOUtils.Read(token, nameof(TopOffset), TopOffset);
        Rounding = IOUtils.Read(token, nameof(Rounding), Rounding);
        OutlineThickness = IOUtils.Read(token, nameof(OutlineThickness), OutlineThickness);
        PrefillStart = IOUtils.Read(token, nameof(PrefillStart), PrefillStart);
        UseMapTime = IOUtils.Read(token, nameof(UseMapTime), UseMapTime);
        IOUtils.ReadRgba(token, "Fill", ref FillR, ref FillG, ref FillB, ref FillA);
        IOUtils.ReadRgba(token, "Back", ref BackR, ref BackG, ref BackB, ref BackA);
        IOUtils.ReadRgba(token, "OutlineCol", ref OutlineColR, ref OutlineColG, ref OutlineColB, ref OutlineColA);
        if(token[nameof(FillGradient)] is JObject fillGradient) FillGradient = StatColor.Deserialize(fillGradient);
    }
}

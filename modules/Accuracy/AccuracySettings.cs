using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.Accuracy;
public sealed class AccuracySettings : ISettingsFile {
    public bool Enabled = true;
    public double WindowMs = 5.7;
    public double MaxDeviationMs = 50.0;
    public double CurveExponent = 1.8;
    public int ComboThreshold = 50;
    public int EmptyPressTolerance = 8;
    public double EmptyPressPenalty = -50;
    public double MissPenalty = -100;
    public double OverloadPenalty = -100;
    public bool ShowHitText = true;
    public bool ShowDeathMarkers = true;
    public JToken Serialize() => new JObject {
        [nameof(Enabled)] = Enabled,
        [nameof(WindowMs)] = WindowMs,
        [nameof(MaxDeviationMs)] = MaxDeviationMs,
        [nameof(CurveExponent)] = CurveExponent,
        [nameof(ComboThreshold)] = ComboThreshold,
        [nameof(EmptyPressTolerance)] = EmptyPressTolerance,
        [nameof(EmptyPressPenalty)] = EmptyPressPenalty,
        [nameof(MissPenalty)] = MissPenalty,
        [nameof(OverloadPenalty)] = OverloadPenalty,
        [nameof(ShowHitText)] = ShowHitText,
        [nameof(ShowDeathMarkers)] = ShowDeathMarkers,
    };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        WindowMs = IOUtils.Read(token, nameof(WindowMs), WindowMs);
        MaxDeviationMs = IOUtils.Read(token, nameof(MaxDeviationMs), MaxDeviationMs);
        CurveExponent = IOUtils.Read(token, nameof(CurveExponent), CurveExponent);
        ComboThreshold = IOUtils.Read(token, nameof(ComboThreshold), ComboThreshold);
        EmptyPressTolerance = IOUtils.Read(token, nameof(EmptyPressTolerance), EmptyPressTolerance);
        EmptyPressPenalty = IOUtils.Read(token, nameof(EmptyPressPenalty), EmptyPressPenalty);
        MissPenalty = IOUtils.Read(token, nameof(MissPenalty), MissPenalty);
        OverloadPenalty = IOUtils.Read(token, nameof(OverloadPenalty), OverloadPenalty);
        ShowHitText = IOUtils.Read(token, nameof(ShowHitText), ShowHitText);
        ShowDeathMarkers = IOUtils.Read(token, nameof(ShowDeathMarkers), ShowDeathMarkers);
    }
}

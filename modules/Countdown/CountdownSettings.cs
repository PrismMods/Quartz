using System;
using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.Countdown;
public sealed class CountdownSettings : ISettingsFile {
    public bool Enabled = true;
    public CountdownMode Mode = CountdownMode.Haywire;
    public float MinBpm = 400f;
    public float MaxBpm = 600f;
    public bool ShowControlPanel = true;
    public bool ShowMetronomeIcon = true;
    public bool AnimatePlanets = true;
    public bool UseCustomBpm = false;
    public float ClickBpm = 300f;
    public int Numerator = 4;
    public int Denominator = 4;
    public float Volume = 1f;
    public JToken Serialize() {
        return new JObject {
            [nameof(Enabled)] = Enabled,
            [nameof(Mode)] = Mode.ToString(),
            [nameof(MinBpm)] = MinBpm,
            [nameof(MaxBpm)] = MaxBpm,
            [nameof(ShowControlPanel)] = ShowControlPanel,
            [nameof(ShowMetronomeIcon)] = ShowMetronomeIcon,
            [nameof(AnimatePlanets)] = AnimatePlanets,
            [nameof(UseCustomBpm)] = UseCustomBpm,
            [nameof(ClickBpm)] = ClickBpm,
            [nameof(Numerator)] = Numerator,
            [nameof(Denominator)] = Denominator,
            [nameof(Volume)] = Volume,
        };
    }
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        Mode = Enum.TryParse(IOUtils.Read(token, nameof(Mode), Mode.ToString()), true, out CountdownMode mode)
            ? mode
            : Mode;
        MinBpm = IOUtils.Read(token, nameof(MinBpm), MinBpm);
        MaxBpm = IOUtils.Read(token, nameof(MaxBpm), MaxBpm);
        if(MaxBpm < MinBpm) MaxBpm = MinBpm;
        ShowControlPanel = IOUtils.Read(token, nameof(ShowControlPanel), ShowControlPanel);
        ShowMetronomeIcon = IOUtils.Read(token, nameof(ShowMetronomeIcon), ShowMetronomeIcon);
        AnimatePlanets = IOUtils.Read(token, nameof(AnimatePlanets), AnimatePlanets);
        UseCustomBpm = IOUtils.Read(token, nameof(UseCustomBpm), UseCustomBpm);
        ClickBpm = IOUtils.Read(token, nameof(ClickBpm), ClickBpm);
        Numerator = IOUtils.Read(token, nameof(Numerator), Numerator);
        Denominator = IOUtils.Read(token, nameof(Denominator), Denominator);
        Volume = IOUtils.Read(token, nameof(Volume), Volume);
    }
}

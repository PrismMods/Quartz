using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.VisualTweaks;
public sealed class VisualTweaksSettings : ISettingsFile {
    public bool RemoveAllCheckpoints = true;
    public bool RemoveBallCoreParticles = true;
    public bool DisableTileHitGlow = true;
    public bool RemovePlanetGlow = true;
    public bool ShowSpeedChangeBpm = false;
    public int SpeedBpmDisplay = 0;
    public int SpeedBpmPosition = 0;
    public JToken Serialize() =>
        new JObject {
            [nameof(RemoveAllCheckpoints)] = RemoveAllCheckpoints,
            [nameof(RemoveBallCoreParticles)] = RemoveBallCoreParticles,
            [nameof(DisableTileHitGlow)] = DisableTileHitGlow,
            [nameof(RemovePlanetGlow)] = RemovePlanetGlow,
            [nameof(ShowSpeedChangeBpm)] = ShowSpeedChangeBpm,
            [nameof(SpeedBpmDisplay)] = SpeedBpmDisplay,
            [nameof(SpeedBpmPosition)] = SpeedBpmPosition,
        };
    public void Deserialize(JToken token) {
        RemoveAllCheckpoints = IOUtils.Read(token, nameof(RemoveAllCheckpoints), RemoveAllCheckpoints);
        RemoveBallCoreParticles = IOUtils.Read(token, nameof(RemoveBallCoreParticles), RemoveBallCoreParticles);
        DisableTileHitGlow = IOUtils.Read(token, nameof(DisableTileHitGlow), DisableTileHitGlow);
        RemovePlanetGlow = IOUtils.Read(token, nameof(RemovePlanetGlow), RemovePlanetGlow);
        ShowSpeedChangeBpm = IOUtils.Read(token, nameof(ShowSpeedChangeBpm), ShowSpeedChangeBpm);
        SpeedBpmDisplay = IOUtils.Read(token, nameof(SpeedBpmDisplay), SpeedBpmDisplay);
        SpeedBpmPosition = IOUtils.Read(token, nameof(SpeedBpmPosition), SpeedBpmPosition);
    }
}

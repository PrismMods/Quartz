using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.DeathLimit;
public sealed class DeathLimitSettings : ISettingsFile {
    public bool DeathLimitEnabled = false;
    public bool MaxDeathsOn = true;
    public int MaxDeaths = 10;
    public bool MaxMissesOn = false;
    public int MaxMisses = 3;
    public bool MaxOverloadsOn = false;
    public int MaxOverloads = 3;
    public string DeathLimitMessage = "Exceeded death limit!!";
    public JToken Serialize() => new JObject {
        [nameof(DeathLimitEnabled)] = DeathLimitEnabled,
        [nameof(MaxDeathsOn)] = MaxDeathsOn,
        [nameof(MaxDeaths)] = MaxDeaths,
        [nameof(MaxMissesOn)] = MaxMissesOn,
        [nameof(MaxMisses)] = MaxMisses,
        [nameof(MaxOverloadsOn)] = MaxOverloadsOn,
        [nameof(MaxOverloads)] = MaxOverloads,
        [nameof(DeathLimitMessage)] = DeathLimitMessage,
    };
    public void Deserialize(JToken token) {
        DeathLimitEnabled = IOUtils.Read(token, nameof(DeathLimitEnabled), DeathLimitEnabled);
        MaxDeathsOn = IOUtils.Read(token, nameof(MaxDeathsOn), MaxDeathsOn);
        MaxDeaths = IOUtils.Read(token, nameof(MaxDeaths), MaxDeaths);
        MaxMissesOn = IOUtils.Read(token, nameof(MaxMissesOn), MaxMissesOn);
        MaxMisses = IOUtils.Read(token, nameof(MaxMisses), MaxMisses);
        MaxOverloadsOn = IOUtils.Read(token, nameof(MaxOverloadsOn), MaxOverloadsOn);
        MaxOverloads = IOUtils.Read(token, nameof(MaxOverloads), MaxOverloads);
        DeathLimitMessage = IOUtils.Read(token, nameof(DeathLimitMessage), DeathLimitMessage);
    }
}

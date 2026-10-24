using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.MainMenu;
public sealed class MenuTweaksSettings : ISettingsFile {
    public bool DisableMenuMusic = true;
    public bool MenuBpmEnabled = false;
    public float MenuSlowBpm = 100f;
    public float MenuHighBpm = 200f;
    public JToken Serialize() =>
        new JObject {
            [nameof(DisableMenuMusic)] = DisableMenuMusic,
            [nameof(MenuBpmEnabled)] = MenuBpmEnabled,
            [nameof(MenuSlowBpm)] = MenuSlowBpm,
            [nameof(MenuHighBpm)] = MenuHighBpm,
        };
    public void Deserialize(JToken token) {
        DisableMenuMusic = IOUtils.Read(token, nameof(DisableMenuMusic), DisableMenuMusic);
        MenuBpmEnabled = IOUtils.Read(token, nameof(MenuBpmEnabled), MenuBpmEnabled);
        MenuSlowBpm = IOUtils.Read(token, nameof(MenuSlowBpm), MenuSlowBpm);
        MenuHighBpm = IOUtils.Read(token, nameof(MenuHighBpm), MenuHighBpm);
    }
}

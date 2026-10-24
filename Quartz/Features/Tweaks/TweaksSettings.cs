using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.Tweaks;
public sealed class TweaksSettings : ISettingsFile {
    public bool DisableAutoPause = true;
    public bool BlockMouseWheelScrollWhilePlaying = true;
    public JToken Serialize() =>
        new JObject {
            [nameof(DisableAutoPause)] = DisableAutoPause,
            [nameof(BlockMouseWheelScrollWhilePlaying)] = BlockMouseWheelScrollWhilePlaying,
        };
    public void Deserialize(JToken token) {
        DisableAutoPause = IOUtils.Read(token, nameof(DisableAutoPause), DisableAutoPause);
        BlockMouseWheelScrollWhilePlaying = IOUtils.Read(token, nameof(BlockMouseWheelScrollWhilePlaying), BlockMouseWheelScrollWhilePlaying);
    }
}

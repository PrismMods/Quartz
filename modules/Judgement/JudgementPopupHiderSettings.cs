using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.Judgement;
public sealed class JudgementPopupHiderSettings : ISettingsFile {
    public bool Enabled = true;
    public int HiddenMask = 1 << JudgementPopupHider.XPerfectPerfectBit;
    public JToken Serialize() => new JObject {
        [nameof(Enabled)] = Enabled,
        [nameof(HiddenMask)] = HiddenMask,
    };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        HiddenMask = IOUtils.Read(token, nameof(HiddenMask), HiddenMask);
    }
}

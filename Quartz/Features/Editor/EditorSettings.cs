using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.Editor;
public sealed class EditorSettings : ISettingsFile {
    public bool HorizontalProperties = false;
    public bool ShowFloorAngle = true;   
    public bool ShowFloorBeats = false;  
    public bool ShowFloorCount = false;  
    public bool ShowFloorDuration = false; 
    public bool UseTulttakModBehavior = false;
    public bool BgaMod = false;
    public bool BgaHideTileDeco = false;
    public bool BgaHidePlanetDeco = false;
    public bool AdjustOnFlip = false;
    public bool AdjustOnRotate = false;
    public bool CustomAngleRotation = false;
    public float CustomAngle = 90f;
    public bool ShowAny =>
        ShowFloorAngle || ShowFloorBeats || ShowFloorCount || ShowFloorDuration;
    public JToken Serialize() {
        return new JObject {
            [nameof(HorizontalProperties)] = HorizontalProperties,
            [nameof(ShowFloorAngle)] = ShowFloorAngle,
            [nameof(ShowFloorBeats)] = ShowFloorBeats,
            [nameof(ShowFloorCount)] = ShowFloorCount,
            [nameof(ShowFloorDuration)] = ShowFloorDuration,
            [nameof(UseTulttakModBehavior)] = UseTulttakModBehavior,
            [nameof(BgaMod)] = BgaMod,
            [nameof(BgaHideTileDeco)] = BgaHideTileDeco,
            [nameof(BgaHidePlanetDeco)] = BgaHidePlanetDeco,
            [nameof(AdjustOnFlip)] = AdjustOnFlip,
            [nameof(AdjustOnRotate)] = AdjustOnRotate,
            [nameof(CustomAngleRotation)] = CustomAngleRotation,
            [nameof(CustomAngle)] = CustomAngle,
        };
    }
    public void Deserialize(JToken token) {
        if(token == null) return;
        HorizontalProperties = IOUtils.Read(token, nameof(HorizontalProperties), HorizontalProperties);
        ShowFloorAngle = IOUtils.Read(token, nameof(ShowFloorAngle), ShowFloorAngle);
        ShowFloorBeats = IOUtils.Read(token, nameof(ShowFloorBeats), ShowFloorBeats);
        ShowFloorCount = IOUtils.Read(token, nameof(ShowFloorCount), ShowFloorCount);
        ShowFloorDuration = IOUtils.Read(token, nameof(ShowFloorDuration), ShowFloorDuration);
        UseTulttakModBehavior = IOUtils.Read(token, nameof(UseTulttakModBehavior), UseTulttakModBehavior);
        BgaMod = IOUtils.Read(token, nameof(BgaMod), BgaMod);
        BgaHideTileDeco = IOUtils.Read(token, nameof(BgaHideTileDeco), BgaHideTileDeco);
        BgaHidePlanetDeco = IOUtils.Read(token, nameof(BgaHidePlanetDeco), BgaHidePlanetDeco);
        AdjustOnFlip = IOUtils.Read(token, nameof(AdjustOnFlip), AdjustOnFlip);
        AdjustOnRotate = IOUtils.Read(token, nameof(AdjustOnRotate), AdjustOnRotate);
        CustomAngleRotation = IOUtils.Read(token, nameof(CustomAngleRotation), CustomAngleRotation);
        CustomAngle = IOUtils.Read(token, nameof(CustomAngle), CustomAngle);
    }
}

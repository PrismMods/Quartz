using Quartz.Interop;
using UnityEngine;
namespace Quartz.Features.PlanetColors;
public sealed class PlanetColorsImport : IImportHandler {
    public int Apply(ImportSource source) => source.Kind switch {
        ImportSourceKind.KorenResourcePackV1 => ApplyV1(source),
        ImportSourceKind.JipperResourcePack => ApplyJipper(source),
        _ => 0,
    };
    private static int ApplyJipper(ImportSource source) {
        if(!source.TryBool("ChangeBallColor", out bool on)) return 0;
        PlanetColors.EnsureConf();
        PlanetColors.Conf.Enabled = on;
        return 1;
    }
    private static int ApplyV1(ImportSource source) {
        PlanetColors.EnsureConf();
        PlanetColorsSettings planet = PlanetColors.Conf;
        int count = 0;
        if(source.TryBool("ChangeBallColor", out bool ballOn)) {
            planet.Enabled = ballOn;
            count++;
        }
        for(int slot = 0; slot < 3; slot++) {
            string prefix = "BallPlanet" + (slot + 1);
            if(source.Color(prefix) is { } ballColor) {
                planet.SetBallRgb(slot, ballColor);
                count++;
            }
            if(source.TryFloat(prefix + "Opacity", out float opacity)) {
                planet.BallOpacity[slot] = Mathf.Clamp01(opacity);
                count++;
            }
        }
        if(source.TryBool("ChangeRingColor", out bool ringOn)) {
            planet.EnableRingRecolor = ringOn;
            count++;
        }
        if(source.Color("Ring") is { } ringColor) {
            planet.SetRingRgb(ringColor);
            count++;
        }
        return count;
    }
    public void Refresh() {
        PlanetColors.EnsureConf();
        PlanetColors.Refresh();
        PlanetColors.Save();
    }
}

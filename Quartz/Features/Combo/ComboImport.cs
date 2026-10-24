using Quartz.Interop;
namespace Quartz.Features.Combo;
public sealed class ComboImport : IImportHandler {
    public int Apply(ImportSource source) => source.Kind switch {
        ImportSourceKind.KorenResourcePackV1 => ApplyV1(source),
        ImportSourceKind.JipperResourcePack => ApplyShaped(source),
        _ => 0,
    };
    private static int ApplyShaped(ImportSource source) {
        int count = 0;
        if(source.TryExtra(ImportKeys.ComboEnabled, out bool on)) {
            ComboOverlay.EnsureConf();
            ComboOverlay.Conf.Enabled = on;
            count++;
        }
        if(source.TryExtra(ImportKeys.ComboCountAuto, out bool auto)) {
            ComboOverlay.EnsureConf();
            ComboOverlay.Conf.CountAuto = auto;
            count++;
        }
        if(source.TryExtra(ImportKeys.ComboColorMax, out int colorMax)) {
            ComboOverlay.EnsureConf();
            ComboOverlay.Conf.ColorMax = colorMax;
            count++;
        }
        if(source.TryExtra(ImportKeys.ComboColorLow, out UnityEngine.Color low)
            && source.TryExtra(ImportKeys.ComboColorHigh, out UnityEngine.Color high)) {
            ComboOverlay.EnsureConf();
            ComboOverlay.Conf.SetColorLow(low);
            ComboOverlay.Conf.SetColorHigh(high);
            count++;
        }
        return count;
    }
    private static int ApplyV1(ImportSource source) {
        ComboOverlay.EnsureConf();
        int count = 0;
        if(source.TryBool("comboOn", out bool on)) {
            ComboOverlay.Conf.Enabled = on;
            count++;
        }
        if(source.TryBool("EnableAutoCombo", out bool auto)) {
            ComboOverlay.Conf.CountAuto = auto;
            count++;
        }
        if(source.TryInt("ComboColorMax", out int colorMax)) {
            ComboOverlay.Conf.ColorMax = colorMax;
            count++;
        }
        if(source.TryBool("XPerfectComboEnabled", out bool xperfect)) {
            ComboOverlay.Conf.XPerfectComboEnabled = xperfect;
            count++;
        }
        if(source.Color("ComboColorLow") is { } low) {
            ComboOverlay.Conf.SetColorLow(low);
            count++;
        }
        if(source.Color("ComboColorHigh") is { } high) {
            ComboOverlay.Conf.SetColorHigh(high);
            count++;
        }
        return count;
    }
    public void Refresh() {
        ComboOverlay.EnsureConf();
        ComboOverlay.Apply();
        ComboOverlay.Save();
    }
}

using Quartz.Interop;
namespace Quartz.Features.ProgressBar;
public sealed class ProgressBarImport : IImportHandler {
    public int Apply(ImportSource source) => source.Kind switch {
        ImportSourceKind.KorenResourcePackV1 => ApplyV1(source),
        ImportSourceKind.JipperResourcePack => ApplyShaped(source),
        _ => 0,
    };
    private static int ApplyShaped(ImportSource source) {
        int count = 0;
        if(source.TryExtra(ImportKeys.ProgressBarEnabled, out bool on)) {
            ProgressBarOverlay.EnsureConf();
            ProgressBarOverlay.Conf.Enabled = on;
            count++;
        }
        if(source.TryExtra(ImportKeys.ProgressBarFill, out UnityEngine.Color fill)) {
            ProgressBarOverlay.EnsureConf();
            ProgressBarOverlay.Conf.SetFillColor(fill);
            count++;
        }
        if(source.TryExtra(ImportKeys.ProgressBarBack, out UnityEngine.Color back)) {
            ProgressBarOverlay.EnsureConf();
            ProgressBarOverlay.Conf.SetBackColor(back);
            count++;
        }
        if(source.TryExtra(ImportKeys.ProgressBarBorder, out UnityEngine.Color border)) {
            ProgressBarOverlay.EnsureConf();
            ProgressBarOverlay.Conf.SetOutlineColor(border);
            count++;
        }
        return count;
    }
    private static int ApplyV1(ImportSource source) {
        ProgressBarOverlay.EnsureConf();
        int count = 0;
        if(source.TryBool("progressBarOn", out bool on)) {
            ProgressBarOverlay.Conf.Enabled = on;
            count++;
        }
        if(source.Color("ProgressBarFill") is { } fill) {
            ProgressBarOverlay.Conf.SetFillColor(fill);
            count++;
        }
        if(source.Color("ProgressBarBack") is { } back) {
            ProgressBarOverlay.Conf.SetBackColor(back);
            count++;
        }
        if(source.Color("ProgressBarBorder") is { } border) {
            ProgressBarOverlay.Conf.SetOutlineColor(border);
            count++;
        }
        return count;
    }
    public void Refresh() {
        ProgressBarOverlay.EnsureConf();
        ProgressBarOverlay.Apply();
        ProgressBarOverlay.Save();
    }
}

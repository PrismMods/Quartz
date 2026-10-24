using Quartz.Interop;
namespace Quartz.Features.Judgement;
public sealed class JudgementImport : IImportHandler {
    public int Apply(ImportSource source) => source.Kind switch {
        ImportSourceKind.KorenResourcePackV1 => ApplyV1(source),
        ImportSourceKind.JipperResourcePack => ApplyShaped(source),
        _ => 0,
    };
    private static int ApplyShaped(ImportSource source) {
        int count = 0;
        if(source.TryExtra(ImportKeys.JudgementEnabled, out bool on)) {
            JudgementOverlay.EnsureConf();
            JudgementOverlay.Conf.Enabled = on;
            count++;
        }
        if(source.TryExtra(ImportKeys.JudgementOffsetY, out float offsetY)) {
            JudgementOverlay.EnsureConf();
            JudgementOverlay.Conf.OffsetY = offsetY;
            count++;
        }
        return count;
    }
    private static int ApplyV1(ImportSource source) {
        JudgementOverlay.EnsureConf();
        int count = 0;
        if(source.TryBool("judgementOn", out bool on)) {
            JudgementOverlay.Conf.Enabled = on;
            count++;
        }
        if(source.TryFloat("judgementPositionY", out float offsetY)) {
            JudgementOverlay.Conf.OffsetY = offsetY;
            count++;
        }
        if(source.TryFloat("judgementSize", out float size)) {
            JudgementOverlay.Conf.Size = size;
            count++;
        }
        if(source.TryFloat("judgementSpacing", out float spacing)) {
            JudgementOverlay.Conf.Spacing = spacing;
            count++;
        }
        return count;
    }
    public void Refresh() {
        JudgementOverlay.EnsureConf();
        JudgementOverlay.Apply();
        JudgementOverlay.Save();
    }
}

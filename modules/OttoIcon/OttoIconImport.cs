using Quartz.Interop;
namespace Quartz.Features.OttoIcon;
public sealed class OttoIconImport : IImportHandler {
    public int Apply(ImportSource source) => source.Kind switch {
        ImportSourceKind.KorenResourcePackV1 => ApplyV1(source),
        ImportSourceKind.JipperResourcePack => ApplyJipper(source),
        _ => 0,
    };
    private static int ApplyJipper(ImportSource source) {
        if(!source.TryBool("ChangeRabbit", out bool on)) return 0;
        OttoIcon.EnsureConf();
        OttoIcon.Conf.Enabled = on;
        return 1;
    }
    private static int ApplyV1(ImportSource source) {
        OttoIcon.EnsureConf();
        int count = 0;
        if(source.TryBool("ChangeOttoIcon", out bool on)) {
            OttoIcon.Conf.Enabled = on;
            count++;
        }
        if(source.Color("Otto") is { } color) {
            OttoIcon.Conf.SetColor(color);
            count++;
        }
        if(source.TryFloat("OttoOffsetX", out float x)) {
            OttoIcon.Conf.OffsetX = x;
            count++;
        }
        if(source.TryFloat("OttoOffsetY", out float y)) {
            OttoIcon.Conf.OffsetY = y;
            count++;
        }
        return count;
    }
    public void Refresh() {
        OttoIcon.EnsureConf();
        OttoIcon.Refresh();
        OttoIcon.Save();
    }
}

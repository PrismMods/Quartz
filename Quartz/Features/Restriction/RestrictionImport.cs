using Quartz.Interop;
namespace Quartz.Features.Restriction;
public sealed class RestrictionImport : IImportHandler {
    public int Apply(ImportSource source) => source.Kind switch {
        ImportSourceKind.KorenResourcePackV1 => ApplyV1(source),
        ImportSourceKind.AdofaiTweaks => ApplyMask(source),
        _ => 0,
    };
    private static int ApplyMask(ImportSource source) {
        if(!source.TryExtra(ImportKeys.RestrictionAllowedMask, out int allowedMask)) return 0;
        Restriction.EnsureConf();
        Restriction.Conf.JRestrictEnabled = true;
        Restriction.Conf.JRestrictMode = 3;
        Restriction.Conf.JRestrictAllowedMask = allowedMask;
        return 3;
    }
    private static int ApplyV1(ImportSource source) {
        Restriction.EnsureConf();
        RestrictionSettings c = Restriction.Conf;
        int count = 0;
        void Flag(string name, Action<bool> set) {
            if(!source.TryBool(name, out bool value)) return;
            set(value);
            count++;
        }
        void Number(string name, Action<int> set) {
            if(!source.TryInt(name, out int value)) return;
            set(value);
            count++;
        }
        Flag("JRestrictOn", v => c.JRestrictEnabled = v);
        Number("JRestrictMode", v => c.JRestrictMode = v);
        if(source.TryFloat("JRestrictAccuracy", out float accuracy)) {
            c.JRestrictAccuracy = accuracy;
            count++;
        }
        Number("JRestrictAllowedMask", v => c.JRestrictAllowedMask = v);
        return count;
    }
    public void Refresh() {
        Restriction.EnsureConf();
        Restriction.Save();
    }
}

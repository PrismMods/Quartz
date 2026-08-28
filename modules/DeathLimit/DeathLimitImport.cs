using Quartz.Interop;
namespace Quartz.Features.DeathLimit;
public sealed class DeathLimitImport : IImportHandler {
    public int Apply(ImportSource source) =>
        source.Kind == ImportSourceKind.KorenResourcePackV1 ? ApplyV1(source) : 0;
    private static int ApplyV1(ImportSource source) {
        DeathLimit.EnsureConf();
        DeathLimitSettings c = DeathLimit.Conf;
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
        Flag("DeathLimitOn", v => c.DeathLimitEnabled = v);
        Flag("DeathLimitMaxDeathsOn", v => c.MaxDeathsOn = v);
        Number("DeathLimitMaxDeaths", v => c.MaxDeaths = v);
        Flag("DeathLimitMaxMissesOn", v => c.MaxMissesOn = v);
        Number("DeathLimitMaxMisses", v => c.MaxMisses = v);
        Flag("DeathLimitMaxOverloadsOn", v => c.MaxOverloadsOn = v);
        Number("DeathLimitMaxOverloads", v => c.MaxOverloads = v);
        return count;
    }
    public void Refresh() {
        DeathLimit.EnsureConf();
        DeathLimit.Save();
    }
}

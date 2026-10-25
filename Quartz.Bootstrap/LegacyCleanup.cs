using System.IO;
namespace Quartz.Bootstrap;
// Removes the pre-bootstrap layout (payload in Mods/) that the loader would load twice. Locked files are renamed .old and swept next launch.
public static class LegacyCleanup {
    // Property, not the const: an inline const compare is constant-false in the KeyViewer flavour (CS0162 as error).
    private static bool FullFlavor => BootstrapInfo.ModName == "Quartz";
    // A pre-bootstrap DLL was in Mods/ and MelonLoader already loaded it: don't load the payload too.
    public static bool RetireMelonLeftovers(string modsDir, string userLibsDir, Action<string> warn) {
        bool legacyLoaded = Retire(Path.Combine(modsDir, BootstrapInfo.PayloadFileName), warn);
        if(FullFlavor) {
            legacyLoaded |= Retire(Path.Combine(modsDir, "Koren.dll"), warn);
            legacyLoaded |= Retire(Path.Combine(modsDir, "Quartz.Loader.ML.dll"), warn);
            Retire(Path.Combine(userLibsDir, "Quartz.dll"), warn);
        }
        return legacyLoaded;
    }
    public static void RetireUmmLeftovers(string modPath, Action<string> warn) {
        Retire(Path.Combine(modPath, BootstrapInfo.PayloadFileName), warn);
    }
    private static bool Retire(string path, Action<string> warn) {
        SweepOld(path + ".old", warn);
        if(!File.Exists(path)) return false;
        try {
            File.Delete(path);
            warn($"removed the pre-bootstrap {Path.GetFileName(path)} — it is superseded by the versioned runtime");
        } catch(Exception deleteError) {
            _ = deleteError.Message;
            try {
                File.Move(path, path + ".old");
                warn($"retired the pre-bootstrap {Path.GetFileName(path)} to .old — it will be removed next launch");
            } catch(Exception moveError) {
                warn("could not retire " + path + ": " + moveError.Message);
            }
        }
        return true;
    }
    private static void SweepOld(string oldPath, Action<string> warn) {
        if(!File.Exists(oldPath)) return;
        try {
            File.Delete(oldPath);
        } catch(Exception e) {
            warn("could not remove " + oldPath + ": " + e.Message);
        }
    }
}

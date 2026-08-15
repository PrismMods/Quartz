using System.IO;
namespace Quartz.Bootstrap;
// Removes the pre-bootstrap install layout, where the payload itself sat in the
// loader's scan path. A release upgrade extracted by the old in-game updater
// leaves that stale payload beside the new bootstrap, and the loader would
// load both. A loaded DLL can't always be deleted, so locked files are retired
// to .old and the .old is swept on the next launch.
public static class LegacyCleanup {
    // A property, not the const directly: comparing the baked ModName const
    // inline makes the branch constant-false in the KeyViewer flavour and the
    // warning gate turns the resulting CS0162 into an error.
    private static bool FullFlavor => BootstrapInfo.ModName == "Quartz";
    // True when a pre-bootstrap mod DLL sat in Mods/ this launch. MelonLoader
    // has already registered it, so the caller must NOT load the payload too:
    // two same-identity Quartz assemblies in one process leave byte-loaded
    // modules free to bind the dormant one. The old mod runs its final session
    // and the retire below makes the next launch bootstrap-only.
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

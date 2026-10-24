using Quartz.Core;
namespace Quartz.IO;
public static partial class ProfileManager {
    // Settings whose code defaults carry content (keys, panels); Clean writes them empty.
    private static readonly Dictionary<string, string> CleanFiles = new(StringComparer.OrdinalIgnoreCase) {
        ["Settings.json"] = """{"IsFirstRun":false}""",
        // "editor" mode skips the legacy migration that would seed a default key layout.
        ["KeyViewer.json"] = """{"Mode":"editor"}""",
        ["OverlayPanels.json"] = """{"Panels":[]}""",
        ["KeyLimiter.json"] = """{"Profiles":[{"Name":"Profile 1","Keys":[]}]}""",
    };
    /// <summary>
    /// Resets the active profile's live settings in place, like a profile switch.
    /// Reset: every setting back to its fresh-install default. Clean: the same, then
    /// settings that would seed content (key layouts, panels, key lists) start empty.
    /// Installed modules, addons, other profiles and play counts are left alone.
    /// </summary>
    public static bool ResetLive(bool clean) {
        string language = MainCore.Conf.Language;
        bool languageDetected = MainCore.Conf.LanguageAutoDetected;
        Dictionary<string, string> previousFiles = null;
        bool runtimeStopped = false;
        bool switchStarted = false;
        try {
            if(!CaptureActive()) throw new IOException("could not capture the active profile before resetting");
            previousFiles = ReadSettingsDirectory(MainCore.Paths.RootPath, validateJson: false);
            BeginSwitch(Active, previousFiles);
            switchStarted = true;
            SettingsRegistry.CancelPendingSaves();
            MainCore.Runtime.SetModEnabled(false, true);
            runtimeStopped = true;
            ReplaceLiveSettings(new Dictionary<string, string>());
            SettingsRegistry.ReloadAll();
            // A reset shouldn't switch the UI language out from under the user.
            MainCore.Conf.Language = language;
            MainCore.Conf.LanguageAutoDetected = languageDetected;
            if(clean) {
                // Partial files only override fields they name, so they go on top of the defaults.
                ReplaceLiveSettings(CleanFiles);
                SettingsRegistry.ReloadAll();
            }
            // Write the reset state now so a later capture or quit can't bring the old files back.
            if(!SettingsRegistry.SaveAll()) throw new IOException("could not save the reset settings");
            ApplyRuntimeSettings();
            MainCore.Runtime.SetModEnabled(MainCore.Conf.Active, true);
            CompleteSwitch();
            return true;
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(ProfileManager)}] Reset ({(clean ? "clean" : "defaults")}) failed: {e}");
            if(previousFiles != null) {
                try {
                    ReplaceLiveSettings(previousFiles);
                    SettingsRegistry.ReloadAll();
                    ApplyRuntimeSettings();
                    if(runtimeStopped) MainCore.Runtime.SetModEnabled(MainCore.Conf.Active, true);
                    if(switchStarted) CompleteSwitch();
                } catch(Exception rollbackError) {
                    MainCore.Log.Err($"[{nameof(ProfileManager)}] Reset rollback failed: {rollbackError}");
                }
            }
            return false;
        }
    }
}

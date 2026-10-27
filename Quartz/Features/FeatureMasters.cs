using Newtonsoft.Json.Linq;
using Quartz.Core;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features;
// These features used to have their own Enabled toggle on their page as well as the Features tab switch.
// The Features tab switch is now the only one: a saved false turns the feature off there, and loading the
// feature sets the field back to true so the runtime checks never block a feature that is on.
internal static class FeatureMasters {
    internal static readonly (string Id, string FileName, string Field)[] Table = [
        ("keyviewer", "KeyViewer.json", "Enabled"),
        ("combo", "Combo.json", "Enabled"),
        ("progressbar", "ProgressBar.json", "Enabled"),
        ("songtitle", "SongTitle.json", "Enabled"),
        ("judgement", "Judgement.json", "Enabled"),
        ("hidejudgements", "JudgementPopupHider.json", "Enabled"),
        ("planetcolors", "PlanetColors.json", "Enabled"),
        ("ottoicon", "OttoIcon.json", "Enabled"),
        ("keylimiter", "KeyLimiter.json", "Enabled"),
        ("effectremover", "EffectRemover.json", "On"),
        ("uihider", "UiHider.json", "Enabled"),
        ("practice", "Practice.json", "Enabled"),
        ("autodeafen", "AutoDeafen.json", "Enabled"),
        ("countdown", "Countdown.json", "Enabled"),
        ("tilearc", "TileArc.json", "Enabled"),
        ("deathlimit", "DeathLimit.json", "DeathLimitEnabled"),
        ("restriction", "Restriction.json", "JRestrictEnabled"),
    ];
    // shortcut: an import that writes false while the feature runs leaves it idle until the next start, when this
    // turns it off; route imported on/off through FeatureService.SetEnabled if that ever matters.
    internal static bool Migrate(FeatureState state, Func<string, JObject> read) {
        bool changed = false;
        foreach((string id, string fileName, string field) in Table) {
            if(!state.IsEnabled(id)) continue;
            JObject settings;
            try {
                settings = read(fileName);
            } catch(Exception e) {
                Diag.Ignore(e);
                continue;
            }
            if(settings?[field] is not { Type: JTokenType.Boolean } flag || (bool)flag) continue;
            state.Features[id] = false;
            changed = true;
        }
        return changed;
    }
    internal static JObject ReadFile(string fileName) {
        string path = System.IO.Path.Combine(MainCore.Paths.RootPath, fileName);
        return System.IO.File.Exists(path) ? JObject.Parse(System.IO.File.ReadAllText(path)) : null;
    }
    // Called from OnLoad: loading means the Features tab has the feature on.
    internal static void Claim<T>(SettingsFile<T> file, bool on, Action<T> turnOn) where T : class, ISettingsFile, new() {
        if(on || file == null) return;
        turnOn(file.Data);
        file.Save();
    }
}

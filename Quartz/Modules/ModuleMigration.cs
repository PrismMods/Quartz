using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Modules;
public static partial class ModuleMigration {
    public sealed class Rule {
        public string Id;
        public string FileName;
        public string Field;
        public bool Default;
    }
    public static readonly Rule[] Table = [
        new Rule { Id = "overlay", FileName = "Overlay.json" },
        new Rule { Id = "keyviewer", FileName = "KeyViewer.json", Field = "Enabled", Default = true },
        new Rule { Id = "panels", FileName = "OverlayPanels.json", Field = "Enabled", Default = true },
        new Rule { Id = "combo", FileName = "Combo.json", Field = "Enabled", Default = true },
        new Rule { Id = "progressbar", FileName = "ProgressBar.json", Field = "Enabled", Default = true },
        new Rule { Id = "songtitle", FileName = "SongTitle.json", Field = "Enabled", Default = true },
        new Rule { Id = "judgement", FileName = "Judgement.json", Field = "Enabled", Default = true },
        new Rule { Id = "hidejudgements", FileName = "JudgementPopupHider.json", Field = "Enabled", Default = true },
        new Rule { Id = "planetcolors", FileName = "PlanetColors.json", Field = "Enabled", Default = true },
        new Rule { Id = "ottoicon", FileName = "OttoIcon.json", Field = "Enabled", Default = true },
        new Rule { Id = "keylimiter", FileName = "ChatterBlocker.json", Field = "Enabled", Default = true },
        new Rule { Id = "keylimiter", FileName = "KeyLimiter.json", Field = "Enabled", Default = true },
        new Rule { Id = "effectremover", FileName = "EffectRemover.json", Field = "On", Default = true },
        new Rule { Id = "uihider", FileName = "UiHider.json", Field = "Enabled", Default = false },
        new Rule { Id = "practice", FileName = "Practice.json", Field = "Enabled", Default = false },
        new Rule { Id = "autodeafen", FileName = "AutoDeafen.json", Field = "Enabled", Default = false },
        new Rule { Id = "countdown", FileName = "Countdown.json", Field = "Enabled", Default = true },
        new Rule { Id = "tweaks", FileName = "Tweaks.json" },
        new Rule { Id = "mainmenu", FileName = "Tweaks.json" },
        new Rule { Id = "visualtweaks", FileName = "Tweaks.json" },
        new Rule { Id = "restriction", FileName = "Restriction.json" },
        new Rule { Id = "deathlimit", FileName = "Restriction.json" },
        new Rule { Id = "calibration", FileName = "Calibration.json" },
        new Rule { Id = "optimizer", FileName = "Optimizer.json" },
        new Rule { Id = "editor", FileName = "Editor.json" },
        new Rule { Id = "nostalgia", FileName = "Nostalgia.json" },
        new Rule { Id = "nostalgia-gameplay", FileName = "Nostalgia.json" },
        new Rule { Id = "nostalgia-visuals", FileName = "Nostalgia.json" },
        new Rule { Id = "nostalgia-tweaks", FileName = "Nostalgia.json" },
        new Rule { Id = "nostalgia-editor", FileName = "Nostalgia.json" },
        new Rule { Id = "tuf", FileName = "TUF/Settings.json" },
        new Rule { Id = "discord", FileName = "Discord.json", Field = "Enabled", Default = false },
        new Rule { Id = "minecraft", FileName = "Minecraft.json", Field = "Enabled", Default = false },
        new Rule { Id = "accuracy", FileName = "Accuracy.json", Field = "Enabled", Default = false },
        new Rule { Id = "tilearc", FileName = "TileArc.json", Field = "Enabled", Default = false },
    ];
    public sealed class Split {
        public string From;
        public string To;
    }
    public static readonly Split[] Splits = [
        new Split { From = "judgement", To = "hidejudgements" },
        new Split { From = "tweaks", To = "mainmenu" },
        new Split { From = "tweaks", To = "visualtweaks" },
        new Split { From = "restriction", To = "deathlimit" },
    ];
    public static bool NeedsSourceRefresh(string installed, string bundled) {
        if(string.IsNullOrEmpty(installed) || string.IsNullOrEmpty(bundled)) return false;
        return SemVer.Compare(installed, bundled) < 0;
    }
    public sealed class Refresh {
        public string Id;
        public string From;
        public string To;
    }
    public static List<Refresh> PlanRefresh(
        IReadOnlyList<ModuleManifest> bundled, Func<string, string> installedVersion
    ) {
        List<Refresh> stale = [];
        if(bundled == null || installedVersion == null) return stale;
        foreach(ModuleManifest manifest in bundled) {
            if(manifest?.Id == null) continue;
            string installed;
            try {
                installed = installedVersion(manifest.Id);
            } catch(Exception e) {
                Diag.Ignore(e);
                continue;
            }
            if(!NeedsSourceRefresh(installed, manifest.Version)) continue;
            stale.Add(new Refresh { Id = manifest.Id, From = installed, To = manifest.Version });
        }
        return stale;
    }
    public sealed class Plan {
        public bool IsUpgrade;
        public readonly List<string> Install = [];
    }
    public static Plan Decide(Func<string, JObject> read) {
        Plan plan = new();
        Dictionary<string, JObject> seen = new(StringComparer.Ordinal);
        JObject Read(string fileName) {
            if(seen.TryGetValue(fileName, out JObject cached)) return cached;
            JObject value;
            try {
                value = read?.Invoke(fileName);
            } catch(Exception e) {
                Diag.Ignore(e);
                value = null;
            }
            seen[fileName] = value;
            return value;
        }
        foreach(Rule rule in Table) {
            if(Read(rule.FileName) == null) continue;
            plan.IsUpgrade = true;
            break;
        }
        if(!plan.IsUpgrade) return plan;
        foreach(Rule rule in Table) {
            if(plan.Install.Contains(rule.Id)) continue;
            if(Wants(rule, Read(rule.FileName))) plan.Install.Add(rule.Id);
        }
        return plan;
    }
    private static bool Wants(Rule rule, JObject settings) {
        if(string.IsNullOrEmpty(rule.Field)) return true;
        if(settings == null) return rule.Default;
        return settings[rule.Field] is { Type: JTokenType.Boolean } flag ? (bool)flag : rule.Default;
    }
}

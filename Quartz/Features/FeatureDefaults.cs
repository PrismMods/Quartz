using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Features;
// Default on/off per feature: from 1.x settings files if present, else fresh-install defaults.
public static class FeatureDefaults {
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
        new Rule { Id = "antichatter", FileName = "ChatterBlocker.json", Field = "Enabled", Default = true },
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
        new Rule { Id = "tuf", FileName = "TUF/Settings.json" },
        new Rule { Id = "tilearc", FileName = "TileArc.json", Field = "Enabled", Default = false },
    ];
    public sealed class Plan {
        public bool IsUpgrade;
        public readonly List<string> Enable = [];
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
            if(plan.Enable.Contains(rule.Id)) continue;
            if(Wants(rule, Read(rule.FileName))) plan.Enable.Add(rule.Id);
        }
        return plan;
    }
    private static bool Wants(Rule rule, JObject settings) {
        if(string.IsNullOrEmpty(rule.Field)) return true;
        if(settings == null) return rule.Default;
        return settings[rule.Field] is { Type: JTokenType.Boolean } flag ? (bool)flag : rule.Default;
    }
    public static bool FreshDefault(string id) {
        bool known = false;
        foreach(Rule rule in Table) {
            if(rule.Id != id) continue;
            known = true;
            if(Wants(rule, null)) return true;
        }
        return !known;
    }
}

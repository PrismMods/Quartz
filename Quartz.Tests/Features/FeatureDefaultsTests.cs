using Newtonsoft.Json.Linq;
using Quartz.Features;
using static Asserts;
static class FeatureDefaultsTests {
    private static Func<string, JObject> Files(params (string Name, string Json)[] files) =>
        name => {
            foreach((string Name, string Json) file in files)
                if(file.Name == name) return JObject.Parse(file.Json);
            return null;
        };
    public static void TestFreshInstallIsNotAnUpgrade() {
        FeatureDefaults.Plan plan = FeatureDefaults.Decide(_ => null);
        Assert(!plan.IsUpgrade, "a data root with no feature settings is a fresh install, not an upgrade");
        Assert(plan.Enable.Count == 0, "a fresh install carries nothing over");
    }
    public static void TestUpgradeKeepsEnabledFeaturesAndDropsDisabledOnes() {
        FeatureDefaults.Plan plan = FeatureDefaults.Decide(Files(
            ("KeyViewer.json", """{"Enabled":true}"""),
            ("Combo.json", """{"Enabled":false}"""),
            ("OverlayPanels.json", """{"Enabled":true}""")));
        Assert(plan.IsUpgrade, "one existing settings file is enough to prove an existing install");
        Assert(plan.Enable.Contains("keyviewer"), "an enabled feature is carried over");
        Assert(!plan.Enable.Contains("combo"), "a feature the user turned off is not carried over");
        Assert(plan.Enable.Contains("panels"), "Panels is read from OverlayPanels.json, not Panels.json");
    }
    public static void TestAbsentFileFallsBackToTheFeatureDefault() {
        FeatureDefaults.Plan plan = FeatureDefaults.Decide(Files(("KeyViewer.json", """{"Enabled":true}""")));
        Assert(plan.Enable.Contains("songtitle"), "a default-on feature with no settings file is carried over");
        Assert(!plan.Enable.Contains("autodeafen"), "Auto Deafen defaults off, so an absent file must not install it");
        Assert(!plan.Enable.Contains("uihider") && !plan.Enable.Contains("practice"),
            "every default-off feature follows the same rule");
    }
    public static void TestMalformedOrPartialSettingsUseTheDefault() {
        FeatureDefaults.Plan plan = FeatureDefaults.Decide(name => name switch {
            "KeyViewer.json" => JObject.Parse("""{"OffsetX":10}"""),
            "AutoDeafen.json" => JObject.Parse("""{"Enabled":"yes"}"""),
            "EffectRemover.json" => JObject.Parse("""{"On":false}"""),
            _ => null,
        });
        Assert(plan.Enable.Contains("keyviewer"), "a settings file missing the flag falls back to the default");
        Assert(!plan.Enable.Contains("autodeafen"), "a non-boolean flag falls back to the default rather than being truthy");
        Assert(!plan.Enable.Contains("effectremover"), "Effect Remover's master flag is named On, not Enabled");
    }
    public static void TestFeaturesWithNoMasterToggleAlwaysComeAlong() {
        FeatureDefaults.Plan plan = FeatureDefaults.Decide(Files(("KeyViewer.json", "{}")));
        foreach(string id in new[] { "tweaks", "restriction", "calibration", "optimizer", "editor", "tuf" })
            Assert(plan.Enable.Contains(id), $"'{id}' has no master toggle, so an upgrade keeps it");
    }
    public static void TestJudgementAndHideJudgementsCarryOverSeparately() {
        FeatureDefaults.Plan off = FeatureDefaults.Decide(Files(
            ("Judgement.json", """{"Enabled":false}"""),
            ("JudgementPopupHider.json", """{"Enabled":false}""")));
        Assert(!off.Enable.Contains("judgement") && !off.Enable.Contains("hidejudgements"),
            "both halves off means neither module is carried over");
        FeatureDefaults.Plan half = FeatureDefaults.Decide(Files(
            ("Judgement.json", """{"Enabled":false}"""),
            ("JudgementPopupHider.json", """{"Enabled":true}""")));
        Assert(half.Enable.Contains("hidejudgements"), "the popup hider carries over on its own");
        Assert(!half.Enable.Contains("judgement"),
            "the overlay counter stays behind — the popup hider no longer drags it along");
        Assert(half.Enable.FindAll(id => id == "hidejudgements").Count == 1, "a module is never planned twice");
    }
    public static void TestAnUnreadableSettingsFileNeverStopsTheMigration() {
        FeatureDefaults.Plan plan = FeatureDefaults.Decide(name =>
            name == "KeyViewer.json" ? throw new IOException("locked") : name == "Combo.json" ? JObject.Parse("{}") : null);
        Assert(plan.IsUpgrade, "the readable files still prove this is an upgrade");
        Assert(plan.Enable.Contains("keyviewer"), "a file that cannot be read falls back to the default");
        Assert(plan.Enable.Contains("combo"), "the rest of the table is still applied");
    }
    public static void TestFreshInstallUsesEachFeaturesDefault() {
        Assert(FeatureDefaults.FreshDefault("keyviewer"), "a default-on feature starts on");
        Assert(FeatureDefaults.FreshDefault("tweaks"), "a feature with no master toggle starts on");
        Assert(!FeatureDefaults.FreshDefault("discord") && !FeatureDefaults.FreshDefault("uihider"),
            "a default-off feature starts off");
        Assert(FeatureDefaults.FreshDefault("some-new-feature"), "a feature with no rule starts on");
    }
    public static void TestEveryRuleTargetsAValidFeatureId() {
        foreach(FeatureDefaults.Rule rule in FeatureDefaults.Table) {
            Assert(System.Text.RegularExpressions.Regex.IsMatch(rule.Id, "^[a-z0-9-]+$"), $"'{rule.Id}' is a valid feature id");
            Assert(!string.IsNullOrWhiteSpace(rule.FileName), $"'{rule.Id}' names the settings file it reads");
            Assert(!rule.FileName.Contains(".."), $"'{rule.Id}' cannot read outside the data root");
        }
    }
}

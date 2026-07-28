using Newtonsoft.Json.Linq;
using Quartz.Modules;
using static Asserts;
static class ModuleMigrationTests {
    private static Func<string, JObject> Files(params (string Name, string Json)[] files) =>
        name => {
            foreach((string Name, string Json) file in files)
                if(file.Name == name) return JObject.Parse(file.Json);
            return null;
        };
    public static void TestFreshInstallStaysCoreOnly() {
        ModuleMigration.Plan plan = ModuleMigration.Decide(_ => null);
        Assert(!plan.IsUpgrade, "a data root with no feature settings is a fresh install, not an upgrade");
        Assert(plan.Install.Count == 0, "a fresh install carries nothing over");
    }
    public static void TestUpgradeKeepsEnabledFeaturesAndDropsDisabledOnes() {
        ModuleMigration.Plan plan = ModuleMigration.Decide(Files(
            ("KeyViewer.json", """{"Enabled":true}"""),
            ("Combo.json", """{"Enabled":false}"""),
            ("OverlayPanels.json", """{"Enabled":true}""")));
        Assert(plan.IsUpgrade, "one existing settings file is enough to prove an existing install");
        Assert(plan.Install.Contains("keyviewer"), "an enabled feature is carried over");
        Assert(!plan.Install.Contains("combo"), "a feature the user turned off is not carried over");
        Assert(plan.Install.Contains("panels"), "Panels is read from OverlayPanels.json, not Panels.json");
    }
    public static void TestAbsentFileFallsBackToTheFeatureDefault() {
        ModuleMigration.Plan plan = ModuleMigration.Decide(Files(("KeyViewer.json", """{"Enabled":true}""")));
        Assert(plan.Install.Contains("songtitle"), "a default-on feature with no settings file is carried over");
        Assert(!plan.Install.Contains("autodeafen"), "Auto Deafen defaults off, so an absent file must not install it");
        Assert(!plan.Install.Contains("uihider") && !plan.Install.Contains("practice"),
            "every default-off feature follows the same rule");
    }
    public static void TestMalformedOrPartialSettingsUseTheDefault() {
        ModuleMigration.Plan plan = ModuleMigration.Decide(name => name switch {
            "KeyViewer.json" => JObject.Parse("""{"OffsetX":10}"""),
            "AutoDeafen.json" => JObject.Parse("""{"Enabled":"yes"}"""),
            "EffectRemover.json" => JObject.Parse("""{"On":false}"""),
            _ => null,
        });
        Assert(plan.Install.Contains("keyviewer"), "a settings file missing the flag falls back to the default");
        Assert(!plan.Install.Contains("autodeafen"), "a non-boolean flag falls back to the default rather than being truthy");
        Assert(!plan.Install.Contains("effectremover"), "Effect Remover's master flag is named On, not Enabled");
    }
    public static void TestFeaturesWithNoMasterToggleAlwaysComeAlong() {
        ModuleMigration.Plan plan = ModuleMigration.Decide(Files(("KeyViewer.json", "{}")));
        foreach(string id in new[] { "tweaks", "restriction", "calibration", "optimizer", "editor", "nostalgia", "tuf" })
            Assert(plan.Install.Contains(id), $"'{id}' has no master toggle, so an upgrade keeps it");
    }
    public static void TestJudgementAndHideJudgementsCarryOverSeparately() {
        ModuleMigration.Plan off = ModuleMigration.Decide(Files(
            ("Judgement.json", """{"Enabled":false}"""),
            ("JudgementPopupHider.json", """{"Enabled":false}""")));
        Assert(!off.Install.Contains("judgement") && !off.Install.Contains("hidejudgements"),
            "both halves off means neither module is carried over");
        ModuleMigration.Plan half = ModuleMigration.Decide(Files(
            ("Judgement.json", """{"Enabled":false}"""),
            ("JudgementPopupHider.json", """{"Enabled":true}""")));
        Assert(half.Install.Contains("hidejudgements"), "the popup hider carries over on its own");
        Assert(!half.Install.Contains("judgement"),
            "the overlay counter stays behind — the popup hider no longer drags it along");
        Assert(half.Install.FindAll(id => id == "hidejudgements").Count == 1, "a module is never planned twice");
    }
    public static void TestASplitAlsoRefreshesTheModuleItWasSplitOutOf() {
        Assert(ModuleMigration.NeedsSourceRefresh("2.0.0-alpha-90", "2.0.0-alpha-93"),
            "a source module older than the bundled copy still owns the split-out page, so it is replaced");
        Assert(!ModuleMigration.NeedsSourceRefresh("2.0.0-alpha-93", "2.0.0-alpha-93"),
            "an already-current source module is left alone");
        Assert(!ModuleMigration.NeedsSourceRefresh("2.0.1", "2.0.0-alpha-93"),
            "a newer source module — say one installed from the catalog — is never downgraded");
        Assert(!ModuleMigration.NeedsSourceRefresh(null, "2.0.0-alpha-93")
            && !ModuleMigration.NeedsSourceRefresh("2.0.0-alpha-90", null),
            "an unreadable version on either side leaves the install untouched");
    }
    public static void TestEverySplitTargetsValidModuleIds() {
        foreach(ModuleMigration.Split split in ModuleMigration.Splits) {
            Assert(ModuleManifest.IsValidId(split.From), $"'{split.From}' is a loadable module id");
            Assert(ModuleManifest.IsValidId(split.To), $"'{split.To}' is a loadable module id");
            Assert(split.From != split.To, "a module cannot be split out of itself");
        }
    }
    public static void TestAnUnreadableSettingsFileNeverStopsTheMigration() {
        ModuleMigration.Plan plan = ModuleMigration.Decide(name =>
            name == "KeyViewer.json" ? throw new IOException("locked") : name == "Combo.json" ? JObject.Parse("{}") : null);
        Assert(plan.IsUpgrade, "the readable files still prove this is an upgrade");
        Assert(plan.Install.Contains("keyviewer"), "a file that cannot be read falls back to the default");
        Assert(plan.Install.Contains("combo"), "the rest of the table is still applied");
    }
    public static void TestEveryRuleTargetsAValidModuleId() {
        foreach(ModuleMigration.Rule rule in ModuleMigration.Table) {
            Assert(ModuleManifest.IsValidId(rule.Id), $"'{rule.Id}' is a loadable module id");
            Assert(!string.IsNullOrWhiteSpace(rule.FileName), $"'{rule.Id}' names the settings file it reads");
            Assert(!rule.FileName.Contains(".."), $"'{rule.Id}' cannot read outside the data root");
        }
    }
}

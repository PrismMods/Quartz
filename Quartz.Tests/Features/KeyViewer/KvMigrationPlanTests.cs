using Quartz.Features.KeyViewer.Layout;
using static Asserts;
static class KvMigrationPlanTests {
    private const string DmPreset = """
        {
          "selectedKeyType": "4key",
          "keys": { "4key": ["Z", "X"] },
          "keyPositions": {
            "4key": [
              { "dx": 0, "dy": 0, "width": 60, "height": 60, "count": 0 },
              { "dx": 70, "dy": 0, "width": 60, "height": 60, "count": 0 }
            ]
          }
        }
        """;
    public static void TestOnlyLegacyModesTriggerAMigration() {
        Assert(KvMigrationPlan.Decide("simple", null, null) == KvMigrationSource.Simple, "simple migrates");
        Assert(KvMigrationPlan.Decide("SIMPLE", null, null) == KvMigrationSource.Simple, "mode match is case-insensitive");
        Assert(KvMigrationPlan.Decide("dmnote", DmPreset, null) == KvMigrationSource.DmNote, "dmnote migrates from its preset");
        Assert(KvMigrationPlan.Decide("native", DmPreset, null) == KvMigrationSource.None, "the current mode never migrates");
        Assert(KvMigrationPlan.Decide(null, DmPreset, null) == KvMigrationSource.None, "no mode, no migration");
        Assert(KvMigrationPlan.Decide("", DmPreset, null) == KvMigrationSource.None, "empty mode, no migration");
    }
    public static void TestAnAuthoredLayoutIsNeverOverwritten() {
        KvDocument authored = KvDocument.Parse(DmPreset);
        Assert(!KvMigrationPlan.IsEmpty(authored), "the parsed preset has elements");
        Assert(KvMigrationPlan.Decide("simple", null, authored) == KvMigrationSource.None,
            "a layout that already has elements is left alone");
        Assert(KvMigrationPlan.Decide("dmnote", DmPreset, authored) == KvMigrationSource.None,
            "same for dmnote — migration only fills an empty layout");
    }
    public static void TestDmNoteFallsBackToSimpleWhenThePresetIsUnusable() {
        Assert(KvMigrationPlan.Decide("dmnote", null, null) == KvMigrationSource.Simple, "no preset");
        Assert(KvMigrationPlan.Decide("dmnote", "   ", null) == KvMigrationSource.Simple, "blank preset");
        Assert(KvMigrationPlan.Decide("dmnote", "{ not json", null) == KvMigrationSource.Simple, "unparseable preset");
        Assert(KvMigrationPlan.Decide("dmnote", "{}", null) == KvMigrationSource.Simple, "a preset with no elements");
    }
    public static void TestEmptinessIsMeasuredAcrossEveryTab() {
        Assert(KvMigrationPlan.IsEmpty(null), "null is empty");
        Assert(KvMigrationPlan.IsEmpty(KvDocument.Empty()), "a fresh document is empty");
        KvDocument document = KvDocument.Empty();
        document.EnsureTab("extra");
        Assert(KvMigrationPlan.IsEmpty(document), "an added but unpopulated tab is still empty");
        KvDocument populated = KvDocument.Parse(DmPreset);
        Assert(!KvMigrationPlan.IsEmpty(populated), "elements in any tab make it non-empty");
    }
    public static void TestFromDmPresetKeepsTheSelectedTabWhenItExists() {
        KvDocument fromPreset = KvMigrationPlan.FromDmPreset(DmPreset, "4key");
        Assert(fromPreset != null, "a usable preset converts");
        Assert(fromPreset.SelectedTab == "4key", "an existing tab is selected");
        KvDocument unknownTab = KvMigrationPlan.FromDmPreset(DmPreset, "no-such-tab");
        Assert(unknownTab != null, "an unknown tab does not fail the conversion");
        Assert(unknownTab.SelectedTab != "no-such-tab", "a tab the preset lacks is not selected");
        Assert(KvMigrationPlan.FromDmPreset(null, "4key") == null, "no preset, no document");
        Assert(KvMigrationPlan.FromDmPreset("{ not json", "4key") == null, "unparseable preset, no document");
        Assert(KvMigrationPlan.FromDmPreset("{}", "4key") == null, "an empty preset yields nothing to migrate");
    }
}

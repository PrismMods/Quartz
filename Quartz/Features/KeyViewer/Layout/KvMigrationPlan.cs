namespace Quartz.Features.KeyViewer.Layout;
internal enum KvMigrationSource {
    None,
    Simple,
    DmNote,
}
internal static class KvMigrationPlan {
    internal const string LegacyModeSimple = "simple";
    internal const string LegacyModeDmNote = "dmnote";
    internal static KvMigrationSource Decide(string mode, string dmPresetJson, KvDocument layout) {
        if(!IsLegacyMode(mode)) return KvMigrationSource.None;
        if(!IsEmpty(layout)) return KvMigrationSource.None;
        return string.Equals(mode, LegacyModeDmNote, StringComparison.OrdinalIgnoreCase)
            && !IsEmpty(TryParse(dmPresetJson))
                ? KvMigrationSource.DmNote
                : KvMigrationSource.Simple;
    }
    private static bool IsLegacyMode(string mode) =>
        string.Equals(mode, LegacyModeSimple, StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, LegacyModeDmNote, StringComparison.OrdinalIgnoreCase);
    internal static KvDocument FromDmPreset(string dmPresetJson, string selectedTab) {
        KvDocument doc = TryParse(dmPresetJson);
        if(IsEmpty(doc)) return null;
        if(doc.HasTab(selectedTab)) doc.SelectedTab = selectedTab;
        return doc;
    }
    internal static bool IsEmpty(KvDocument layout) {
        if(layout == null) return true;
        foreach(string tab in layout.Tabs)
            if(layout.AllElements(tab).Count > 0) return false;
        return true;
    }
    private static KvDocument TryParse(string json) {
        if(string.IsNullOrWhiteSpace(json)) return null;
        try {
            return KvDocument.Parse(json);
        } catch {
            return null;
        }
    }
}

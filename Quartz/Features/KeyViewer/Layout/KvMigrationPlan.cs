namespace Quartz.Features.KeyViewer.Layout;
/// <summary>Which legacy description of the key viewer a migration should rebuild the layout from.</summary>
internal enum KvMigrationSource {
    /// <summary>Nothing to do: already migrated, or a layout is already there to protect.</summary>
    None,
    /// <summary>The flat 36-slot settings model — style, bindings, labels, ghosts, counts.</summary>
    Simple,
    /// <summary>The imported DM Note preset the user was rendering.</summary>
    DmNote,
}
/// <summary>
/// The migration decision, kept engine-free so the fork it makes is covered by Quartz.Tests.
/// <see cref="KvMigration"/> holds the half that needs Unity (the flat model bakes KeyCode and
/// Color); everything here is string and document work.
/// </summary>
internal static class KvMigrationPlan {
    /// <summary>
    /// The two values <see cref="KeyViewerSettings.Mode"/> could hold before the editor replaced
    /// both modes. They live here rather than on the settings class because migration is now their
    /// only reader — nothing branches on the mode at runtime any more.
    /// </summary>
    internal const string LegacyModeSimple = "simple";
    internal const string LegacyModeDmNote = "dmnote";
    /// <summary>
    /// What to rebuild <paramref name="layout"/> from, given the mode the user's last launch was in.
    ///
    /// Two independent guards, because either alone is wrong. The mode is the durable one-shot
    /// marker: <see cref="KvMigration.RunOnce"/> stamps it to editor whatever it decides, so this
    /// answers anything but <see cref="KvMigrationSource.None"/> exactly once in a config's life.
    /// The layout check is what protects a user who reaches that one evaluation already holding a
    /// layout — they used the editor before the update and switched back — from having it
    /// overwritten by settings they abandoned.
    /// </summary>
    internal static KvMigrationSource Decide(string mode, string dmPresetJson, KvDocument layout) {
        if(!IsLegacyMode(mode)) return KvMigrationSource.None;
        if(!IsEmpty(layout)) return KvMigrationSource.None;
        // Only a DM Note user with a preset that still parses into something has a DM Note layout
        // to port. Everyone else falls back to the flat model, which every config carries — for a
        // fresh install those are the stock defaults, so first run generates the stock layout
        // through the same path rather than leaving the viewer empty.
        return string.Equals(mode, LegacyModeDmNote, StringComparison.OrdinalIgnoreCase)
            && !IsEmpty(TryParse(dmPresetJson))
                ? KvMigrationSource.DmNote
                : KvMigrationSource.Simple;
    }
    private static bool IsLegacyMode(string mode) =>
        string.Equals(mode, LegacyModeSimple, StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, LegacyModeDmNote, StringComparison.OrdinalIgnoreCase);
    /// <summary>
    /// The DM Note preset the user was rendering, as a layout, or null when it is not one.
    ///
    /// What survives is bounded by <see cref="KeyViewerPersistence.SanitizeDmPreset"/>, which every
    /// stored preset has already been through: keys, key/stat/graph positions and the selected tab.
    /// customCSS, fontSettings, knobPositions, customTabs and the embedded font/image/sound blobs
    /// were dropped on the way in and are not recoverable here. Tab names go with customTabs, so a
    /// ported tab shows its raw id. The user's own custom CSS is not affected — that lives in
    /// DmCssText, outside the preset.
    /// </summary>
    internal static KvDocument FromDmPreset(string dmPresetJson, string selectedTab) {
        KvDocument doc = TryParse(dmPresetJson);
        if(IsEmpty(doc)) return null;
        // The DM Note renderer resolved this and wrote it back on every render, so it names the tab
        // the user was actually looking at — a truer answer than the preset's own selectedKeyType,
        // which the getter falls back to on its own when this names no tab here.
        if(doc.HasTab(selectedTab)) doc.SelectedTab = selectedTab;
        return doc;
    }
    /// <summary>
    /// True when nothing describes a layout: no document, or one whose every tab is bare. An absent
    /// layout file parses to an empty document, so this is the check to make right after loading one.
    /// </summary>
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
            // Unreadable counts as absent. For the layout file that means the settings are the only
            // surviving description of the user's key viewer, so regenerating from them beats
            // leaving nothing; for a preset it means there is no DM Note layout to port.
            return null;
        }
    }
}

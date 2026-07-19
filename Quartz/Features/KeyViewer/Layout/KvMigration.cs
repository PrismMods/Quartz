using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.KeyViewer.Layout;
/// <summary>
/// One-way import of the two modes the editor replaced — the flat simple-mode
/// <see cref="KeyViewerSettings"/> model and the imported DM Note preset — into the free-form
/// layout the editor owns.
///
/// <see cref="KvPresets"/> generates the layout the user's style drew, palette included.
/// What the flat 36-slot model held on top of that — bindings, labels, ghost keys, press
/// counts — is baked into the elements here, because once the layout is free-form no slot
/// exists to look any of it up by. <see cref="KvMigrationPlan"/> holds the fork and the
/// DM Note half, which need no engine.
/// </summary>
internal static class KvMigration {
    /// <summary>
    /// Rebuild the layout from whichever mode this config was last in, once and only once.
    ///
    /// Automatic rather than a button: the modes it ports from no longer render, so a user who
    /// never pressed it would find their key viewer gone after updating. It runs from
    /// <see cref="KeyViewerOverlay.EnsureConf"/>, which resolves before anything draws and is
    /// never on the gameplay path — the layout file must not be written mid-run.
    ///
    /// Safe to call again: the mode stamp below makes the next call a no-op, and
    /// <see cref="KvMigrationPlan.Decide"/> refuses to overwrite a layout that already exists
    /// even on the one call that can act.
    /// </summary>
    internal static void RunOnce(KeyViewerSettings conf) {
        if(conf == null) return;
        KvMigrationSource source;
        try {
            source = KvMigrationPlan.Decide(conf.Mode, conf.DmPresetJson, KvStore.Current);
        } catch(Exception e) {
            // Never take the overlay down over this. A config left un-stamped is retried next
            // launch, which is the better failure: the alternative is a permanently empty viewer.
            MainCore.Log.Err("[KeyViewer] Layout migration check failed: " + e);
            return;
        }
        if(source == KvMigrationSource.None) {
            // Stamped even when nothing was ported, so "which mode was this user in" is asked once
            // ever rather than re-evaluated on every launch against settings they no longer use.
            StampMigrated(conf);
            return;
        }
        try {
            KvDocument doc = source == KvMigrationSource.DmNote
                ? KvMigrationPlan.FromDmPreset(conf.DmPresetJson, conf.DmSelectedTab)
                : null;
            // The DM Note port is best-effort: Decide only proved the preset parses, and a preset
            // holding no tab the layout can use is not a reason to leave the viewer empty.
            doc ??= FromLegacy(conf);
            KvStore.Replace(doc);
            KvStore.Save();
            StampMigrated(conf);
            MainCore.Log.Msg(
                $"[KeyViewer] Migrated the {(source == KvMigrationSource.DmNote ? "DM Note preset" : "Simple mode settings")} "
                + $"into the layout editor ({doc.AllElements(doc.SelectedTab).Count} elements on tab \"{doc.SelectedTab}\")."
            );
        } catch(Exception e) {
            MainCore.Log.Err("[KeyViewer] Layout migration failed: " + e);
        }
    }
    /// <summary>
    /// Record that this config has been through migration. The legacy fields are left exactly as
    /// they are — a user who rolls back to an alpha that still has the old modes must find their
    /// settings intact, and this stamp is the only thing that changes for them.
    /// </summary>
    private static void StampMigrated(KeyViewerSettings conf) {
        if(conf.Mode == KeyViewerSettings.ModeEditor) return;
        conf.Mode = KeyViewerSettings.ModeEditor;
        KeyViewerOverlay.Save();
    }
    /// <summary>
    /// The layout <paramref name="conf"/>'s flat model describes, as a document. Read-only:
    /// nothing about <paramref name="conf"/> is mutated, so a caller can hold the old settings
    /// back as a rollback until the new layout is written.
    ///
    /// The live settings, deliberately — this is the one place they are the right source, because
    /// it is reconstructing a layout the user really did configure. See
    /// <see cref="GenerateStockTab"/> for the case that is not.
    /// </summary>
    internal static KvDocument FromLegacy(KeyViewerSettings conf) {
        KvDocument doc = KvDocument.Empty();
        if(conf == null) return doc;
        string tab = doc.SelectedTab;
        GenerateTab(doc, tab, conf.Style, conf);
        SetFootRow(doc, tab, conf.FootKeyCount(), conf);
        return doc;
    }
    /// <summary>
    /// "Add an 8-key tab" — <paramref name="style"/>'s layout bound to the STOCK bindings for
    /// that style, never the user's.
    ///
    /// The distinction from <see cref="FromLegacy"/> is the whole point of these two entry
    /// points existing separately, and it is easy to get backwards. Migration carries the user's
    /// edits because it is reconstructing a layout they already configured. Adding a tab is not:
    /// the user asked for "8 Keys", which means the stock 8-key layout, not their 8-key
    /// rebindings from a mode that no longer exists. Stock defaults are still fully bound, so
    /// this does not reintroduce the unbound-tab problem that made the live conf look correct.
    /// </summary>
    internal static void GenerateStockTab(KvDocument doc, string tab, int style) {
        // The 24-key preset is editor-only and lives outside the legacy style axis (its extra
        // row has no slots to bake); the stock 16-key portion still bakes normally.
        if(style == KvPresets.Style24) {
            KvPresets.Generate24KeyTab(doc, tab, (el, slot) => BakeKey(KvPresets.Stock, el, 2, slot));
            return;
        }
        // The 108-key full keyboard binds every key itself from a physical-keyboard table, so it
        // has nothing to bake from the legacy settings — like the 24-key row, but for all of it.
        if(style == KvPresets.Style108) {
            KvPresets.Generate108KeyTab(doc, tab);
            return;
        }
        GenerateTab(doc, tab, style, KvPresets.Stock);
    }
    /// <summary>As above for the foot row: stock foot bindings, never the user's.</summary>
    internal static void SetStockFootRow(KvDocument doc, string tab, int footCount) =>
        SetFootRow(doc, tab, footCount, KvPresets.Stock);
    /// <summary>
    /// Replace <paramref name="tab"/> with <paramref name="style"/>'s layout, carrying the bindings,
    /// labels, ghost keys and counts <paramref name="source"/> holds for that style.
    ///
    /// Private because only two callers may choose <paramref name="source"/>: migration passes the
    /// live settings, tab creation passes <see cref="KvPresets.Stock"/>. Both go through the named
    /// entry points above so the choice is made once, by name, rather than at each call site.
    /// </summary>
    private static void GenerateTab(KvDocument doc, string tab, int style, KeyViewerSettings source) {
        if(doc == null || source == null) return;
        style = Mathf.Clamp(style, 0, KeyViewerSettings.MaxStyle);
        KvPresets.GenerateKeyLayout(doc, tab, style, source, (el, slot) => BakeKey(source, el, style, slot));
    }
    /// <summary>
    /// As above for the foot row: <paramref name="footCount"/> keys under whatever is on the tab,
    /// bound the way <paramref name="source"/> has them for a row of that size, or no row at 0.
    /// </summary>
    private static void SetFootRow(KvDocument doc, string tab, int footCount, KeyViewerSettings source) {
        if(doc == null || source == null) return;
        // The bindings are per foot count, not per the count the user happens to be on
        // (FootKeysByStyle[9][2*s]), so the row being built names its own slice rather than
        // source.FootStyle — which is only the same number when the row matches today's setting.
        int footStyle = Mathf.Clamp(footCount / 2, 0, KeyViewerSettings.MaxFootStyle);
        KvPresets.SetFootRow(doc, tab, footCount, source, (el, slot) => BakeFootKey(source, el, footStyle, slot));
    }
    private static void BakeKey(KeyViewerSettings conf, KvElement el, int style, int slot) {
        // Stats hold no per-slot data: KvPresets has already given them the globals, which
        // is what AddStat resolves them to.
        if(el.Kind != KvElementKind.Key) return;
        int[] keys = conf.KeysForStyle(style);
        if(slot < 0 || slot >= keys.Length) return;
        KeyCode key = (KeyCode)keys[slot];
        el.BindKey(key);
        Label(el, conf.LabelsForStyle(style), slot);
        int[] ghosts = conf.GhostKeysForStyle(style);
        if(slot < ghosts.Length) el.BindGhostKey((KeyCode)ghosts[slot]);
        // AddKey buckets Conf.Counts by Box.Name — the raw Unity enum name, uppercased.
        el.Count = conf.GetCount(key.ToString().ToUpperInvariant());
    }
    private static void BakeFootKey(KeyViewerSettings conf, KvElement el, int footStyle, int slot) {
        int index = slot - KeyViewerSettings.FootSlotBase;
        int[] keys = conf.FootKeysForStyle(footStyle);
        if(index < 0 || index >= keys.Length) return;
        el.BindKey((KeyCode)keys[index]);
        Label(el, conf.FootLabelsForStyle(footStyle), index);
        // No count and no ghost key to carry: a foot key's presses were never counted
        // (AddKey's !IsFoot guard) nor persisted (ShouldPersistBoxCount), and the legacy
        // model has no foot ghost bindings.
    }
    /// <summary>
    /// Bake only a real override. Leaving displayText absent lets the renderer derive the
    /// same short label LabelFor falls back to, so an unlabelled key keeps tracking its
    /// binding instead of freezing today's text.
    /// </summary>
    private static void Label(KvElement el, string[] labels, int index) {
        if(index < labels.Length && !string.IsNullOrEmpty(labels[index])) el.DisplayText = labels[index];
    }
}

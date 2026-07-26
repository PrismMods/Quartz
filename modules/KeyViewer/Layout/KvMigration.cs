using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.KeyViewer.Layout;
internal static class KvMigration {
    internal static void RunOnce(KeyViewerSettings conf) {
        if(conf == null) return;
        KvMigrationSource source;
        try {
            source = KvMigrationPlan.Decide(conf.Mode, conf.DmPresetJson, KvStore.Current);
        } catch(Exception e) {
            MainCore.Log.Err("[KeyViewer] Layout migration check failed: " + e);
            return;
        }
        if(source == KvMigrationSource.None) {
            StampMigrated(conf);
            return;
        }
        try {
            KvDocument doc = source == KvMigrationSource.DmNote
                ? KvMigrationPlan.FromDmPreset(conf.DmPresetJson, conf.DmSelectedTab)
                : null;
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
    private static void StampMigrated(KeyViewerSettings conf) {
        if(conf.Mode == KeyViewerSettings.ModeEditor) return;
        conf.Mode = KeyViewerSettings.ModeEditor;
        KeyViewerOverlay.Save();
    }
    internal static KvDocument FromLegacy(KeyViewerSettings conf) {
        KvDocument doc = KvDocument.Empty();
        if(conf == null) return doc;
        string tab = doc.SelectedTab;
        GenerateTab(doc, tab, conf.Style, conf);
        SetFootRow(doc, tab, conf.FootKeyCount(), conf);
        return doc;
    }
    internal static void GenerateStockTab(KvDocument doc, string tab, int style) {
        if(style == KvPresets.Style24) {
            KvPresets.Generate24KeyTab(doc, tab, (el, slot) => BakeKey(KvPresets.Stock, el, 2, slot));
            return;
        }
        if(style == KvPresets.Style108) {
            KvPresets.Generate108KeyTab(doc, tab);
            return;
        }
        GenerateTab(doc, tab, style, KvPresets.Stock);
    }
    internal static void SetStockFootRow(KvDocument doc, string tab, int footCount) =>
        SetFootRow(doc, tab, footCount, KvPresets.Stock);
    private static void GenerateTab(KvDocument doc, string tab, int style, KeyViewerSettings source) {
        if(doc == null || source == null) return;
        style = Mathf.Clamp(style, 0, KeyViewerSettings.MaxStyle);
        KvPresets.GenerateKeyLayout(doc, tab, style, source, (el, slot) => BakeKey(source, el, style, slot));
    }
    private static void SetFootRow(KvDocument doc, string tab, int footCount, KeyViewerSettings source) {
        if(doc == null || source == null) return;
        int footStyle = Mathf.Clamp(footCount / 2, 0, KeyViewerSettings.MaxFootStyle);
        KvPresets.SetFootRow(doc, tab, footCount, source, (el, slot) => BakeFootKey(source, el, footStyle, slot));
    }
    private static void BakeKey(KeyViewerSettings conf, KvElement el, int style, int slot) {
        if(el.Kind != KvElementKind.Key) return;
        int[] keys = conf.KeysForStyle(style);
        if(slot < 0 || slot >= keys.Length) return;
        KeyCode key = (KeyCode)keys[slot];
        el.BindKey(key);
        Label(el, conf.LabelsForStyle(style), slot);
        int[] ghosts = conf.GhostKeysForStyle(style);
        if(slot < ghosts.Length) el.BindGhostKey((KeyCode)ghosts[slot]);
        el.Count = conf.GetCount(key.ToString().ToUpperInvariant());
    }
    private static void BakeFootKey(KeyViewerSettings conf, KvElement el, int footStyle, int slot) {
        int index = slot - KeyViewerSettings.FootSlotBase;
        int[] keys = conf.FootKeysForStyle(footStyle);
        if(index < 0 || index >= keys.Length) return;
        el.BindKey((KeyCode)keys[index]);
        Label(el, conf.FootLabelsForStyle(footStyle), index);
    }
    private static void Label(KvElement el, string[] labels, int index) {
        if(index < labels.Length && !string.IsNullOrEmpty(labels[index])) el.DisplayText = labels[index];
    }
}

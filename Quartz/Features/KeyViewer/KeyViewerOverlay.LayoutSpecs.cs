using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    /// <summary>
    /// Render specs for the free-form layout the editor owns.
    ///
    /// The layout is stored as a real DM Note preset, so this feeds the same
    /// <see cref="ParseDmNoteSpec"/> and <see cref="FinishDmSpecs"/> the preset path uses:
    /// only the source of the position objects differs, and the two modes cannot drift into
    /// rendering the same document differently.
    ///
    /// Each element is parsed from its own backing object rather than a serialized copy of
    /// the document. That keeps the live <see cref="KvElement"/> reachable for count
    /// write-back, and keeps Build off <see cref="KvDocument.ToJson"/>.
    /// </summary>
    private static List<DmNoteSpec> ParseLayoutSpecs(KvDocument doc) {
        List<DmNoteSpec> result = [];
        dmCanvasHeight = 250f;
        dmCanvasWidth = 800f;
        ApplyDmRuntimeSettings();
        if(doc == null) return result;
        try {
            // The document's own tab, never Conf.DmSelectedTab: writing that here would move
            // DM Note mode's tab selection as a side effect of editing the layout.
            string tab = doc.SelectedTab;
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            foreach(KvElement el in doc.Elements(tab, KvElementKind.Key)) {
                if(el.Hidden) continue;
                DmNoteSpec spec = ParseDmNoteSpec(el.GlobalKey, el.Raw, false);
                AdoptLayoutElement(spec, el);
                result.Add(spec);
                ExtendDmBounds(spec, ref minX, ref minY, ref maxX, ref maxY);
            }
            foreach(KvElement el in doc.Elements(tab, KvElementKind.Stat)) {
                if(el.Hidden) continue;
                DmNoteSpec spec = ParseDmNoteSpec(LayoutStatType(el), el.Raw, true);
                AdoptLayoutElement(spec, el);
                result.Add(spec);
                ExtendDmBounds(spec, ref minX, ref minY, ref maxX, ref maxY);
            }
            foreach(KvElement el in doc.Elements(tab, KvElementKind.Graph)) {
                if(el.Hidden) continue;
                DmNoteSpec spec = ParseGraphSpec(el.Raw);
                // No Source: a graph carries no count and never becomes a Box.
                spec.ZIndex = el.Z;
                result.Add(spec);
                ExtendDmBounds(spec, ref minX, ref minY, ref maxX, ref maxY);
            }
            FinishDmSpecs(result, minX, minY, maxX, maxY);
        } catch(Exception ex) {
            MainCore.Log.Msg("[KeyViewer] Layout parse failed: " + ex.Message);
            result.Clear();
        }
        ApplyCssToSpecs(result);
        return result;
    }
    private static void AdoptLayoutElement(DmNoteSpec spec, KvElement el) {
        spec.Source = el;
        spec.CountInTotal = el.CountInTotal;
        spec.PerKeyKps = el.PerKeyKps;
        spec.ZIndex = el.Z;
    }
    /// <summary>Outer object first, matching the preset path's own statType lookup.</summary>
    private static string LayoutStatType(KvElement el) =>
        JStr(el.Container, "statType", JStr(el.Raw, "statType", "stat"));
}

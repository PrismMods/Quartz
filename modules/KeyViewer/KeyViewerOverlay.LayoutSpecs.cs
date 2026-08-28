using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static List<DmNoteSpec> ParseLayoutSpecs(KvDocument doc, string tab) {
        List<DmNoteSpec> result = [];
        dmCanvasHeight = 250f;
        dmCanvasWidth = 800f;
        if(doc == null || tab == null || !doc.HasTab(tab)) {
            ApplyCssToSpecs(result);
            return result;
        }
        try {
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
                spec.ZIndex = el.Z;
                spec.ClassName = el.ClassName;
                spec.GraphInlineStyles = el.UseInlineStyles;
                result.Add(spec);
                ExtendDmBounds(spec, ref minX, ref minY, ref maxX, ref maxY);
            }
            if(result.Count > 0) {
                if(!doc.TryGetRenderAnchor(tab, out float anchorCx, out float anchorMinY)) {
                    anchorCx = (minX + maxX) * 0.5f;
                    anchorMinY = minY;
                    doc.SetRenderAnchor(tab, anchorCx, anchorMinY);
                    if(ReferenceEquals(doc, KvStore.Current)) KvStore.RequestSave();
                }
                FinishDmSpecs(result, minX, minY, maxX, maxY, anchorCx, anchorMinY);
            }
        } catch(Exception ex) {
            MainCore.Log.Msg("[KeyViewer] Layout parse failed: " + ex.Message);
            result.Clear();
        }
        ApplyCssToSpecs(result);
        return result;
    }
    internal static List<string> RenderTabs(KvDocument doc) {
        List<string> tabs = [];
        if(doc == null) return tabs;
        string hand = doc.SelectedTab;
        tabs.Add(hand);
        string foot = doc.SelectedFootTab;
        if(foot != null && !string.Equals(foot, hand, StringComparison.Ordinal)) tabs.Add(foot);
        return tabs;
    }
    private static void AdoptLayoutElement(DmNoteSpec spec, KvElement el) {
        spec.Source = el;
        spec.CountInTotal = el.CountInTotal;
        spec.PerKeyKps = el.PerKeyKps;
        spec.ZIndex = el.Z;
        spec.ClassName = el.ClassName;
        spec.KeyInlineStyles = el.UseInlineStyles;
    }
    private static string LayoutStatType(KvElement el) =>
        JStr(el.Container, "statType", JStr(el.Raw, "statType", "stat"));
}

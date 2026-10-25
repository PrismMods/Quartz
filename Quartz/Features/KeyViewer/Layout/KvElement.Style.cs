using Newtonsoft.Json.Linq;
namespace Quartz.Features.KeyViewer.Layout;
internal sealed partial class KvElement {
    // Place, binding and content stay with the element; every other field is its look.
    private static readonly HashSet<string> NotStyle = new(StringComparer.Ordinal) {
        "dx", "dy", "width", "height", "zIndex", "hidden", "count", "displayText", "statType", "ghostKey",
        "quartzFoot", "quartzPressedText", "quartzCountInTotal", "quartzPerKeyKps",
    };
    internal JObject CopyStyle() {
        JObject style = [];
        foreach(JProperty p in Raw.Properties())
            if(!NotStyle.Contains(p.Name)) style[p.Name] = p.Value.DeepClone();
        return style;
    }
    // A field the source left unset is removed, so the target falls back to the same default.
    internal void PasteStyle(JObject style) {
        JObject outer = Container;
        bool nested = !ReferenceEquals(outer, Raw);
        List<string> stale = [];
        foreach(JProperty p in Raw.Properties())
            if(!NotStyle.Contains(p.Name) && style[p.Name] == null) stale.Add(p.Name);
        foreach(string key in stale) {
            Raw.Remove(key);
            if(nested) outer.Remove(key);
        }
        foreach(JProperty p in style.Properties()) {
            Raw[p.Name] = p.Value.DeepClone();
            if(nested && outer[p.Name] != null) outer[p.Name] = p.Value.DeepClone();
        }
    }
}

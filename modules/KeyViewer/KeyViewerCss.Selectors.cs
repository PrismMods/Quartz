#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace Quartz.Features.KeyViewer;
public sealed partial class KeyViewerStylesheet {
    public CssKeyStyleSet ResolveKey(string? className) {
        HashSet<string> classes = ClassSet(className);
        var set = new CssKeyStyleSet();
        MapKey(_keyIdle.Flatten(classes), set.Idle);
        MapKey(_keyActive.Flatten(classes), set.Active);
        set.Idle.Before = MapLayer(_beforeIdle.Flatten(classes));
        set.Active.Before = MapLayer(_beforeActive.Flatten(classes));
        set.Idle.After = MapLayer(_afterIdle.Flatten(classes));
        set.Active.After = MapLayer(_afterActive.Flatten(classes));
        return set;
    }
    public CssCounterStyleSet ResolveCounter(string? className) {
        HashSet<string> classes = ClassSet(className);
        var set = new CssCounterStyleSet();
        MapCounter(_ctrIdle.Flatten(classes), set.Idle);
        MapCounter(_ctrActive.Flatten(classes), set.Active);
        return set;
    }
    public CssGraphStyle ResolveGraph(string? className) {
        var style = new CssGraphStyle();
        MapGraph(_graph.Flatten(ClassSet(className)), style);
        return style;
    }
    private static bool HasGraphVar(Dictionary<string, string> decls) {
        foreach(string key in decls.Keys) {
            if(key.StartsWith("--graph-", StringComparison.Ordinal)) { return true; }
        }
        return false;
    }
    private static void MapGraph(Dictionary<string, string> d, CssGraphStyle s) {
        foreach(KeyValuePair<string, string> kv in d) {
            switch(kv.Key) {
                case "--graph-bg":
                    if(TryParseColor(kv.Value, out CssColor bg)) { s.Bg = bg; }
                    break;
                case "--graph-border":
                    ParseGraphBorder(kv.Value, s);
                    break;
                case "--graph-radius":
                    if(TryLen(kv.Value, out float r)) { s.Radius = r; }
                    break;
                case "--graph-color":
                    if(TryParseColor(kv.Value, out CssColor c)) { s.Color = c; }
                    break;
            }
        }
    }
    private static void ParseGraphBorder(string v, CssGraphStyle s) => ParseBorderCore(v, ref s.BorderWidth, ref s.BorderColor);
    private static HashSet<string> ClassSet(string? className) {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(!string.IsNullOrWhiteSpace(className)) {
            foreach(string c in className!.Split(Space, StringSplitOptions.RemoveEmptyEntries)) { set.Add(c); }
        }
        return set;
    }
}

using System.Text.RegularExpressions;
namespace Quartz.Features.Panels;
/// <summary>A panel's classic rows, and so their toggle hotkeys, are keyed by the place its section was built at
/// ("panel2_textshadow"). Unity-free, so it is unit-tested.</summary>
internal static class PanelBinds {
    private static readonly Regex Id = new(@"^panel(\d+)(_.*)$", RegexOptions.CultureInvariant | RegexOptions.Singleline);

    /// <summary>The panel built at place i now sits at to[i], or is gone (-1): each panel's hotkeys move with it, and a deleted
    /// panel's go with it. A bind for a place past to (left by a panel deleted before hotkeys followed their panels) is no
    /// panel's, and stays as it is. True when any bind changed.</summary>
    internal static bool Moved(Dictionary<string, (int Modifier, int Key)> binds, IReadOnlyList<int> to) {
        var moved = new List<(string from, string to, (int, int) bind)>();
        foreach(var kv in binds) {
            Match m = Id.Match(kv.Key);
            if(!m.Success || !int.TryParse(m.Groups[1].Value, out int at) || at >= to.Count) continue;
            string id = to[at] < 0 ? null : "panel" + to[at] + m.Groups[2].Value;
            if(id != kv.Key) moved.Add((kv.Key, id, kv.Value));
        }
        foreach(var b in moved) binds.Remove(b.from);
        foreach(var b in moved)
            if(b.to != null) binds[b.to] = b.bind;
        return moved.Count > 0;
    }
}

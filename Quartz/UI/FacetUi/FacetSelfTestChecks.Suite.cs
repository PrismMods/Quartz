#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using UnityEngine;
namespace Quartz.UI.FacetUi;
/// <summary>The suite look on Quartz's Facet theme: sizes, neutral roles, the accent, and what a window wears.</summary>
internal static partial class FacetSelfTestChecks {
    private static bool Hex(Color c, uint rgb) =>
        Near(c, new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255));
    private static IEnumerator Suite(Action<string, bool, string> check, Action<string> info) {
        Theme th = QuartzHost.Host.Theme;
        Sizes z = th.Sizes;
        Color accent = MainCore.Conf.GetAccentColor();
        Color Text(Color a) => Theme.FromAccent(a)[Role.AccentText];
        check("suite: classic sizes", z.Radius == 10 && z.Body == 16 && z.Title == 22 && z.SwitchWidth == 22,
            $"radius={z.Radius} body={z.Body} title={z.Title} switch={z.SwitchWidth}");
        check("suite: classic roles", Near(th[Role.Panel], UIColors.MenuBG) && Near(th[Role.TitleBar], UIColors.TopBar),
            $"panel={th[Role.Panel]} titlebar={th[Role.TitleBar]}");
        check("suite: the accent", Near(th[Role.Accent], accent) && Near(th[Role.Focus], accent) && Near(th[Role.AccentText], Text(accent)),
            $"accent={th[Role.Accent]} focus={th[Role.Focus]} text={th[Role.AccentText]}");
        Surface s = Window("QuartzSelfTestSuite", out Window w);
        try {
            yield return Frames(2);
            Color bg = w.Root.resolvedStyle.backgroundColor;
            float radius = w.Root.resolvedStyle.borderTopLeftRadius;
            check("suite: a window wears it", radius == 10 && Near(bg, UIColors.MenuBG), $"radius={radius} bg={bg}");
        } finally {
            s.Dispose();
        }
        Color test = Near(accent, Color.red) ? Color.cyan : Color.red;
        QuartzHost.Recolour(test);
        bool mid = Near(th[Role.Accent], test) && Near(th[Role.Focus], test) && Near(th[Role.AccentText], Text(test)) && Hex(th[Role.TitleBar], 0x141418);
        string midDetail = $"accent={th[Role.Accent]} focus={th[Role.Focus]} text={th[Role.AccentText]} titlebar={th[Role.TitleBar]}";
        QuartzHost.Recolour(accent);
        check("suite: Recolour sets the accent in the same frame and keeps the neutrals", mid, midDetail);
        // The accent text is picked by contrast: light on a dark accent, dark on a light one.
        var textOn = new List<(string name, Color accent, bool light)> { ("navy", new Color(.1f, .12f, .35f), true), ("red", Color.red, false), ("Quartz's default", accent, false) };
        var wrong = new List<string>();
        foreach(var (name, c, light) in textOn) {
            QuartzHost.Recolour(c);
            float r = th[Role.AccentText].r;
            if(light ? r <= .8f : r >= .2f) wrong.Add($"{name} r={r:0.##}");
        }
        QuartzHost.Recolour(accent);
        check("suite: a dark accent gets light text on the accent, a light one dark text", wrong.Count == 0, string.Join(", ", wrong));
        check("suite: restoring the accent", Near(th[Role.Accent], accent) && Near(th[Role.Focus], accent) && Near(th[Role.AccentText], Text(accent)),
            $"accent={th[Role.Accent]} text={th[Role.AccentText]}");
    }
}
#endif

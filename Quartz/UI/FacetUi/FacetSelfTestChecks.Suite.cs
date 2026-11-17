#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using UnityEngine;
namespace Quartz.UI.FacetUi;
/// <summary>Quartz's classic skin on its Facet theme: sizes, neutral roles, the accent, and what a window wears.</summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator Suite(Action<string, bool, string> check, Action<string> info) {
        Theme th = QuartzHost.Host.Theme;
        Sizes z = th.Sizes;
        Color accent = MainCore.Conf.GetAccentColor();
        Color Text(Color a) => Theme.FromAccent(a)[Role.AccentText];
        check("suite: classic sizes", z.Radius == 5 && z.Body == 16 && z.Title == 22 && z.SwitchWidth == 16,
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
            check("suite: a window wears it", radius == 5 && Near(bg, UIColors.MenuBG), $"radius={radius} bg={bg}");
        } finally {
            s.Dispose();
        }
        Color test = Near(accent, Color.red) ? Color.cyan : Color.red;
        QuartzHost.Recolour(test);
        bool mid = Near(th[Role.Accent], test) && Near(th[Role.Focus], test) && Near(th[Role.AccentText], Text(test)) && Near(th[Role.TitleBar], UIColors.TopBar) && Near(th[Role.Panel], UIColors.MenuBG);
        string midDetail = $"accent={th[Role.Accent]} focus={th[Role.Focus]} text={th[Role.AccentText]} titlebar={th[Role.TitleBar]} panel={th[Role.Panel]}";
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

#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using Quartz.Prism;
using UnityEngine;
namespace Quartz.UI.FacetUi;
/// <summary>The suite look on Quartz's Facet theme: sizes, neutral roles, the accent, and what a window wears.</summary>
internal static partial class FacetSelfTestChecks {
    private static bool Hex(Color c, uint rgb) =>
        Near(c, new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255));
    private static IEnumerator Suite(Action<string, bool, string> check, Action<string> info) {
        if(!PrismBridge.UiAvailable) {
            info("suite: PrismLib.UI unavailable, Facet's own look");
            check("suite: unsuited without PrismLib.UI", !QuartzHost.Suited, $"suited={QuartzHost.Suited}");
            yield break;
        }
        Theme th = QuartzHost.Host.Theme;
        Sizes z = th.Sizes;
        Color accent = MainCore.Conf.GetAccentColor();
        Color Text(Color a) => Theme.FromAccent(a)[Role.AccentText];
        check("suite: sizes", z.Radius == 2 && z.Body == 13 && z.Title == 17 && z.SwitchWidth == 30,
            $"radius={z.Radius} body={z.Body} title={z.Title} switch={z.SwitchWidth}");
        check("suite: neutral roles", Hex(th[Role.Panel], 0x1A1A1F) && Hex(th[Role.TitleBar], 0x141418),
            $"panel={th[Role.Panel]} titlebar={th[Role.TitleBar]}");
        check("suite: the accent", Near(th[Role.Accent], accent) && Near(th[Role.Focus], accent) && Near(th[Role.AccentText], Text(accent)),
            $"accent={th[Role.Accent]} focus={th[Role.Focus]} text={th[Role.AccentText]}");
        Surface s = Window("QuartzSelfTestSuite", out Window w);
        try {
            yield return Frames(2);
            Color bg = w.Root.resolvedStyle.backgroundColor;
            float radius = w.Root.resolvedStyle.borderTopLeftRadius;
            check("suite: a window wears it", radius == 2 && Hex(bg, 0x1A1A1F), $"radius={radius} bg={bg}");
        } finally {
            s.Dispose();
        }
        Color test = Near(accent, Color.red) ? Color.cyan : Color.red;
        QuartzHost.Recolour(test);
        bool mid = Near(th[Role.Accent], test) && Near(th[Role.Focus], test) && Near(th[Role.AccentText], Text(test)) && Hex(th[Role.TitleBar], 0x141418);
        string midDetail = $"accent={th[Role.Accent]} focus={th[Role.Focus]} text={th[Role.AccentText]} titlebar={th[Role.TitleBar]}";
        QuartzHost.Recolour(accent);
        check("suite: Recolour sets the accent in the same frame and keeps the neutrals", mid, midDetail);
        check("suite: restoring the accent", Near(th[Role.Accent], accent) && Near(th[Role.Focus], accent) && Near(th[Role.AccentText], Text(accent)),
            $"accent={th[Role.Accent]} text={th[Role.AccentText]}");
    }
}
#endif

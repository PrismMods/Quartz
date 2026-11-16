#if DEBUG && !IL2CPP
using System.Collections;
using UnityEngine;
using Quartz.Prism;
using UnityEngine.UIElements;
using HandOff = Quartz.UI.MenuHandOff;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The shortcut menu the menu hotkey opens, opened here through the bridge: its logo and buttons, and where each button goes.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static readonly string[] ShortcutButtons = ["quartz-shortcut-settings", "quartz-shortcut-layout", "quartz-shortcut-close"];

    private static IEnumerator Shortcut(Action<string, bool, string> check, Action<string> info) {
        bool wasOpen = UICore.IsOpen;
        if(wasOpen) UICore.Close(true);
        QuartzShell.Hide();
        HandOff.ReturnTo = HandOff.Menu.None;
        try {
            FacetBridge.OpenShortcut();
            yield return Frames(3);
            VisualElement card = QuartzShortcut.Surface?.Root.Q("quartz-shortcut");
            VisualElement logo = card != null && card.childCount > 0 ? card[0] : null;
            check("shortcut: the window opens and leads with the Quartz logo",
                FacetBridge.ShortcutVisible() && logo != null && logo.name == "quartz-shortcut-logo" && logo.style.backgroundImage.value.sprite != null,
                $"visible={FacetBridge.ShortcutVisible()} first={logo?.name ?? "none"}");
            bool words = card != null && card.childCount == 1 + ShortcutButtons.Length;
            string seen = "";
            for(int i = 0; words && card != null && i < ShortcutButtons.Length; i++) {
                var b = card[i + 1] as Button;
                seen += $" {card[i + 1].name}='{b?.text}'";
                words = b != null && b.name == ShortcutButtons[i] && b.text == QuartzHost.Translate(PrismRules.ShortcutKeys[i]);
            }
            check("shortcut: Settings, Edit layout and Close follow, in Quartz's words", words, seen);
            ShortcutLook(check, info, card);

            SubmitShortcut(0);
            for(int i = 0; i < 30 && !(FacetBridge.NewMenuVisible() && !FacetBridge.ShortcutVisible()); i++) yield return Frames(1);
            check("shortcut: Settings closes it and opens the new menu", FacetBridge.NewMenuVisible() && !FacetBridge.ShortcutVisible(),
                $"menu={FacetBridge.NewMenuVisible()} shortcut={FacetBridge.ShortcutVisible()}");
            QuartzShell.Hide();
            yield return Frames(3);
            check("shortcut: closing that menu does not bring the shortcut menu back", !FacetBridge.ShortcutVisible(), null);

            FacetBridge.OpenShortcut();
            yield return Frames(3);
            SubmitShortcut(2);
            yield return Frames(3);
            check("shortcut: Close closes it and opens nothing",
                !FacetBridge.ShortcutVisible() && !UICore.MenuShowing && !LayoutEditorProbe.Shown(),
                $"shortcut={FacetBridge.ShortcutVisible()} menu={UICore.MenuShowing} editor={LayoutEditorProbe.Shown()}");

            FacetBridge.OpenShortcut();
            yield return Frames(3);
            FacetBridge.HandleEscape();
            yield return Frames(3);
            check("shortcut: Escape closes it", !FacetBridge.ShortcutVisible(), null);

            if(PrismBridge.FilterReady) {
                FacetBridge.OpenShortcut();
                yield return Frames(3);
                SubmitShortcut(1);
                for(int i = 0; i < 30 && !LayoutEditorProbe.Shown(); i++) yield return Frames(1);
                string filter = LayoutEditorProbe.ShownRoot()?.Q("layout-filter")?.userData as string;
                check("shortcut: Edit layout closes it and opens the shared layout editor on Quartz's tab",
                    LayoutEditorProbe.Shown() && !FacetBridge.ShortcutVisible() && filter == "Quartz",
                    $"editor={LayoutEditorProbe.Shown()} shortcut={FacetBridge.ShortcutVisible()} filter={filter ?? "null"}");
                if(LayoutEditorProbe.Shown()) FacetBridge.HandleEscape();
                for(int i = 0; i < 30 && UICore.IsArranging; i++) yield return Frames(1);
            } else info("shortcut: filter off, Edit layout not checked");
        } finally {
            LayoutHotkey.Trip.Cancel();   // the editor Edit layout opened brings nothing back after the section
            FacetBridge.HideShortcut();
            QuartzShell.Hide();
            if(LayoutEditorProbe.Shown()) FacetBridge.HandleEscape();
            HandOff.ReturnTo = HandOff.Menu.None;
            if(wasOpen) UICore.Open(true);
        }
        yield return Frames(3);
    }

    // The hotkey-menu look PrismLib.UI shares: a wash over the whole surface under the window, in the shared colour and
    // letting clicks through; the window with no fill or border; every label centred.
    private static void ShortcutLook(Action<string, bool, string> check, Action<string> info, VisualElement card) {
        if(!QuartzShortcut.Dressed) {
            info("shortcut: no shared hotkey-menu look in this PrismLib.UI, the card stays");
            return;
        }
        VisualElement root = QuartzShortcut.Surface?.Root;
        VisualElement wash = root != null && root.childCount > 0 ? root[0] : null;
        float alpha = PrismBridge.HubWashAlpha;
        string washName = PrismBridge.HubWashName;
        Color c = wash?.resolvedStyle.backgroundColor ?? Color.clear;
        Rect w = wash?.worldBound ?? default, r = root?.worldBound ?? default;
        check("shortcut: a wash under the window covers the whole screen in the shared wash colour",
            wash != null && washName != null && wash.name == washName && root!.IndexOf(card) > 0 && wash.pickingMode == PickingMode.Ignore
            && Mathf.Abs(w.x - r.x) <= 1f && Mathf.Abs(w.y - r.y) <= 1f && Mathf.Abs(w.width - r.width) <= 1f && Mathf.Abs(w.height - r.height) <= 1f
            && Mathf.Abs(c.a - alpha) < 0.01f && c.r + c.g + c.b < 0.01f,
            $"wash={wash?.name ?? "none"} {w} root {r} colour {c} shared alpha {alpha}");
        IResolvedStyle st = card?.resolvedStyle;
        check("shortcut: the window has no fill and no border",
            st != null && st.backgroundColor.a < 0.01f && st.borderLeftWidth == 0f && st.borderRightWidth == 0f && st.borderTopWidth == 0f && st.borderBottomWidth == 0f,
            st == null ? "no card" : $"fill {st.backgroundColor} border {st.borderLeftWidth}/{st.borderTopWidth}/{st.borderRightWidth}/{st.borderBottomWidth}");
        string off = "";
        foreach(string n in ShortcutButtons)
            if(card?.Q<Button>(n) is not Button b || b.resolvedStyle.unityTextAlign != TextAnchor.MiddleCenter) off += " " + n;
        check("shortcut: every button label is centred", off.Length == 0, off);
    }

    // A missing button presses nothing, so the check after it fails instead of the whole run throwing.
    private static void SubmitShortcut(int i) {
        if(QuartzShortcut.Surface?.Root.Q<Button>(ShortcutButtons[i]) is Button b) Submit(b);
    }
}
#endif

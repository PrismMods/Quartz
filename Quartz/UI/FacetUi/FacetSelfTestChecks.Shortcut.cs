#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using UnityEngine;
using Quartz.Prism;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The shortcut menu the menu hotkey opens, opened here through the bridge: its logo and buttons, and where each button goes.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static readonly string[] ShortcutButtons = ["quartz-shortcut-settings", "quartz-shortcut-layout", "quartz-shortcut-close"];

    private static IEnumerator Shortcut(Action<string, bool, string> check, Action<string> info) {
        QuartzShell.Hide();
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
            yield return ShortcutRecolour(check);

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
            yield return ShortcutReopenCost(check, info);

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
        ShortcutHubButtons(check, info, card);
    }

    // The old hub's buttons, logo and wordmark, at PrismLib.UI's shared sizes (read through the bridge, never copied here).
    private static void ShortcutHubButtons(Action<string, bool, string> check, Action<string> info, VisualElement card) {
        var (z, markName) = PrismBridge.HubSizes;
        if(!QuartzShortcut.HubButtons || z == null) {
            info("shortcut: no shared hub buttons in this PrismLib.UI, its own buttons stay");
            return;
        }
        float width = z[0], height = z[1], radius = z[2], text = z[3], logoSize = z[4], markSize = z[5];
        string bad = "";
        for(int i = 0; i < ShortcutButtons.Length; i++) {
            Button b = card?.Q<Button>(ShortcutButtons[i]);
            if(b == null) { bad += $" {ShortcutButtons[i]} missing"; continue; }
            IResolvedStyle st = b.resolvedStyle;
            Color want = i == ShortcutButtons.Length - 1 ? UIColors.ObjectBG : UIColors.ObjectButton;
            Rect r = b.worldBound;
            // Within a screen pixel: the panel snaps edges to whole pixels at its scale.
            if(Mathf.Abs(r.width - width) > 1f || r.height < height - 1f || Mathf.Abs(st.borderTopLeftRadius - radius) > 0.1f
               || Mathf.Abs(st.fontSize - text) > 0.5f || st.backgroundColor.a < 0.999f
               || Mathf.Abs(st.backgroundColor.r - want.r) > 0.01f || Mathf.Abs(st.backgroundColor.g - want.g) > 0.01f || Mathf.Abs(st.backgroundColor.b - want.b) > 0.01f)
                bad += $" {b.name} {r.size} r{st.borderTopLeftRadius} text {st.fontSize} fill {st.backgroundColor}";
        }
        check("shortcut: its buttons have the old hub's size, corners and text on its solid fills, Close neutral", bad.Length == 0, bad);

        VisualElement logo = card?.Q("quartz-shortcut-logo");
        Label mark = logo?.Q<Label>(markName);
        Button first = card?.Q<Button>(ShortcutButtons[0]);
        Rect l = logo?.worldBound ?? default, m = mark?.worldBound ?? default;
        check("shortcut: it leads with the Quartz logo at the shared size and the QUARTZ wordmark under it",
            logo != null && mark != null && first != null && Mathf.Abs(l.width - logoSize) <= 1f && Mathf.Abs(l.height - logoSize) <= 1f
            && mark.text == Info.DisplayName.ToUpperInvariant() && Mathf.Abs(mark.resolvedStyle.fontSize - markSize) <= 0.5f
            && m.y >= l.yMax - 0.5f && m.yMax <= first.worldBound.y + 0.5f && Mathf.Abs(m.center.x - l.center.x) <= 1f && Mathf.Abs(l.center.x - card!.worldBound.center.x) <= 1f,
            $"logo {l} wordmark '{mark?.text}' {m} size {mark?.resolvedStyle.fontSize} first {first?.worldBound}");

        // Korean is the longest of Quartz's words here: each label, measured at the button's text size, fits inside it.
        string over = "";
        for(int i = 0; i < ShortcutButtons.Length; i++) {
            if(card?.Q<Button>(ShortcutButtons[i]) is not Button b) continue;
            string ko = MainCore.Tr.GetForLanguage(PrismRules.ShortcutKeys[i], "ko-KR", b.text);
            Vector2 need = b.MeasureTextSize(ko, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
            if(need.x > b.contentRect.width + 0.5f || need.y > b.contentRect.height + 0.5f) over += $" {b.name} '{ko}' needs {need} in {b.contentRect.size}";
        }
        check("shortcut: the Korean labels fit their buttons", over.Length == 0, over);
    }

    // The classic fills follow the accent: after a change, the reopened menu's buttons wear the new ones.
    private static IEnumerator ShortcutRecolour(Action<string, bool, string> check) {
        if(!QuartzShortcut.HubButtons) yield break;
        Color accent = MainCore.Conf.GetAccentColor(), old = UIColors.ObjectButton;
        FacetBridge.HideShortcut();
        try {
            UIColors.ApplyAccent(new Color(0.15f, 0.55f, 0.95f));
            FacetBridge.OpenShortcut();
            // The fill eases to its new colour: drawn, within a second.
            string bad = "";
            float t0 = Time.realtimeSinceStartup;
            do {
                yield return null;
                bad = "";
                for(int i = 0; i < ShortcutButtons.Length; i++) {
                    Color want = i == ShortcutButtons.Length - 1 ? UIColors.ObjectBG : UIColors.ObjectButton;
                    Color got = QuartzShortcut.Surface?.Root.Q<Button>(ShortcutButtons[i])?.resolvedStyle.backgroundColor ?? Color.clear;
                    if(Mathf.Abs(got.r - want.r) > 0.01f || Mathf.Abs(got.g - want.g) > 0.01f || Mathf.Abs(got.b - want.b) > 0.01f)
                        bad += $" {ShortcutButtons[i]} {got} want {want}";
                }
            } while(bad.Length > 0 && Time.realtimeSinceStartup - t0 < 1f);
            check("shortcut: after an accent change its buttons wear the new accent's fills",
                bad.Length == 0 && Mathf.Abs(UIColors.ObjectButton.r - old.r) + Mathf.Abs(UIColors.ObjectButton.g - old.g) + Mathf.Abs(UIColors.ObjectButton.b - old.b) > 0.1f, bad.Length == 0 ? $"was {old}" : bad);
        } finally {
            UIColors.ApplyAccent(accent);
        }
        yield return Frames(3);
    }

    // Reopening the built shortcut menu: Quartz's and the shared look's own steps (the input block, the fade-in) allocate
    // nothing. They are measured on the shown menu, where Facet's show is a no-op, so Facet's own show and hide (which copy
    // their event's subscriber list on each raise) stay out of the count; nor does the run show a surface thousands of times.
    private static IEnumerator ShortcutReopenCost(Action<string, bool, string> check, Action<string> info) {
        FacetBridge.OpenShortcut();
        yield return Frames(2);
        long own = KvHeapGrowth(() => {
            for(int i = 0; i < KvProbeRuns; i++) FacetBridge.OpenShortcut();
        });
        FacetBridge.HideShortcut();
        info($"[perf] shortcut menu reopen: its own steps grew the heap {own} bytes over {KvProbeRuns} opens");
        check("shortcut: reopening it allocates nothing in its own steps or the shared look's", own < KvProbeSlack, $"{own} bytes");
        yield return Frames(2);
    }

    // A missing button presses nothing, so the check after it fails instead of the whole run throwing.
    private static void SubmitShortcut(int i) {
        if(QuartzShortcut.Surface?.Root.Q<Button>(ShortcutButtons[i]) is Button b) Submit(b);
    }
}
#endif

#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
using System.Collections;
using System.Runtime.CompilerServices;
using PrismLib.UI.Toolkit;
using Quartz.Core;
using Quartz.Features.Calibration;
using Quartz.Features.Combo;
using Quartz.Features.Judgement;
using Quartz.Features.KeyViewer;
using Quartz.Features.Panels;
using Quartz.Features.Practice;
using Quartz.Features.ProgressBar;
using Quartz.Features.SongTitle;
using Quartz.IO;
using Quartz.IO.Interface;
using Quartz.Overlay;
using Quartz.UI;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.Prism;
/// <summary>
/// The arranging state (overlays show while any editor arranges them, and only one editor runs at a time) and Quartz's
/// layout targets, driven through PrismLib's registry as another mod's editor would. PrismLib.dll types stay in the
/// NoInlining helpers at the end.
/// </summary>
internal static partial class PrismSelfTestChecks {
    // n frames, each one in which an editor read Quartz's boxes.
    private static IEnumerator Session(int n) {
        for(int i = 0; i < n; i++) {
            PrismBridge.TouchLayout();
            yield return null;
        }
    }
    internal static IEnumerator Arrange(Action<string, bool, string> check) {
        var flips = new List<bool>();
        Action<bool> rec = flips.Add;
        UICore.OnArrangingChanged += rec;
        try {
            check("arrange: the hotkey works outside a session", UICore.ToggleHotkeyAllowed(true, true), null);
            UICore.EnterReorganize();
            yield return Frames(3);
            check("arrange: reorganize mode is unchanged", UICore.IsArranging && Reorganizer.LiveHandleCount > 0, $"handles={Reorganizer.LiveHandleCount}");
            UICore.ExitReorganize();
            yield return Frames(3);
            yield return Session(3);
            check("arrange: a shared session shows the overlays", UICore.SharedLayoutOpen && GameObject.Find("ComboRoot") != null, null);
            check("arrange: but leaves every drag surface off", Reorganizer.LiveHandleCount == 0, $"handles={Reorganizer.LiveHandleCount}");
            check("arrange: the hotkey is ignored during a session", !UICore.ToggleHotkeyAllowed(true, true), null);
            UICore.EnterReorganize();
            check("arrange: reorganize is refused during a session", !UICore.IsReorganizing, null);
            yield return Frames(4);
            check("arrange: the session ends Grace frames after the last read", !UICore.SharedLayoutOpen, null);
            check("arrange: overlays hide again outside gameplay", GameObject.Find("ComboRoot") == null, null);
            UICore.EnterReorganize();
            yield return Frames(2);
            yield return Session(3);
            check("arrange: a session ends reorganize mode and closes the hub",
                !UICore.IsReorganizing && !UICore.IsOpen && UICore.SharedLayoutOpen && Reorganizer.LiveHandleCount == 0,
                $"reorg={UICore.IsReorganizing} open={UICore.IsOpen} shared={UICore.SharedLayoutOpen} handles={Reorganizer.LiveHandleCount}");
            yield return Frames(4);
        } finally {
            UICore.OnArrangingChanged -= rec;
        }
        check("arrange: OnArrangingChanged fires only on a change", flips.Count >= 4 && flips.Zip(flips.Skip(1), (a, b) => a != b).All(x => x), string.Join(",", flips));
    }
    internal static IEnumerator Layout(Action<string, bool, string> check, Action<string> info) {
        var seen = new List<string>();
        Action<string> log = Ui.Log;
        Ui.Log = m => {
            seen.Add(m);
            log?.Invoke(m);
        };
        (Action Set, Action Finish)[] keep = KeepSettings();
        try {
            string[] required = [LayoutIds.KeyViewer, LayoutIds.Combo, LayoutIds.Judgement, LayoutIds.SongTitle, LayoutIds.Practice, LayoutIds.Calibration, LayoutIds.Panel(0)];
            yield return Session(3);
            string[] mine = [.. Mine().Select(t => t.Id)];
            // What the reorganizer shows, from its own registry: each of those must be a target.
            yield return Frames(4);
            UICore.EnterReorganize();
            yield return Frames(3);
            string[] live = [.. Reorganizer.LiveHandles.Select(h => h.LayoutId ?? "untagged:" + (h.GetName?.Invoke() ?? h.name))];
            UICore.ExitReorganize();
            check("layout: every reorganizer element is a target", live.Length > 0 && live.All(mine.Contains),
                $"reorganizer={string.Join(",", live)} targets={string.Join(",", mine)}");
            string[] off = [.. required.Where(r => !live.Contains(r))];
            if(off.Length > 0) info("layout: not shown by the reorganizer here (feature off): " + string.Join(",", off));
            info("layout: progressbar " + (mine.Contains(LayoutIds.ProgressBar) ? "present" : "absent") + "; targets " + string.Join(",", mine));
            foreach(string id in mine) yield return BothWays(id, check);
            yield return Edges(check, info, seen);
        } finally {
            Ui.Log = log;
            if(UICore.IsReorganizing) UICore.ExitReorganize();
            foreach((Action Set, Action Finish) k in keep) k.Set?.Invoke();
            foreach((Action Set, Action Finish) k in keep) k.Finish?.Invoke();
        }
    }
    // One target, both ways: a shared-editor change lands where the reorganizer reads it, and the reverse.
    private static IEnumerator BothWays(string id, Action<string, bool, string> check) {
        Action unforce = ForceShown(id);
        try {
            EditTarget t = MineById(id);
            yield return Frames(4);
            UICore.EnterReorganize();
            yield return Frames(3);
            ReorganizeHandle h = ReorganizeHandle.FindLayout(id);
            bool shown = h != null && Reorganizer.LiveHandles.Contains(h); // the reorganizer offers it
            UICore.ExitReorganize();
            yield return Session(4);
            float[] b0 = t?.GetBox();
            check($"layout {id}: shown in a session exactly when the reorganizer shows it", t != null && (b0 != null) == shown, $"reorganizer={shown} box={Fmt(b0)}");
            if(t == null || b0 == null || h == null) yield break;
            check($"layout {id}: offers the reorganizer's controls",
                (t.SetRotation != null) == h.CanRotate && (t.SetAnchor != null) == (h.SetAnchor != null)
                && (t.SetAnchorClosest != null) == (h.SetClosest != null) && t.GetCrop == null,
                $"rot={h.CanRotate} anchor={h.SetAnchor != null} closest={h.SetClosest != null}");
            Canvas canvas = h.Target.GetComponentInParent<Canvas>();
            var corners = new Vector3[4];
            h.MeasureRect.GetWorldCorners(corners);
            Vector3 mid = (corners[0] + corners[2]) * 0.5f; // Reorganizer.ScreenCenter
            check($"layout {id}: its box is in screen pixels (a screen-space overlay canvas, centred where the reorganizer measures it)",
                canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay && Near(b0, mid.x, mid.y),
                (canvas == null ? "no canvas" : canvas.rootCanvas.renderMode.ToString()) + $" box={Fmt(b0)} corners=({mid.x:0.0},{mid.y:0.0})");
            float scale0 = t.GetScale(), rot0 = t.GetRotation?.Invoke() ?? 0f;
            int anchor0 = t.GetAnchor?.Invoke() ?? -1;
            bool closest0 = t.GetAnchorClosest?.Invoke() == true;
            // shared editor -> the settings the reorganizer reads
            t.MoveTo(b0[0] + 40f, b0[1] + 30f);
            Reapply(id); // the same frame: the move is already in the settings, not only on the RectTransform
            yield return Session(2);
            Reapply(id);
            yield return Session(2);
            float[] b1 = t.GetBox();
            check($"layout {id}: a shared-editor move is saved at once where the reorganizer reads it", Near(b1, b0[0] + 40f, b0[1] + 30f), Fmt(b0) + " -> " + Fmt(b1));
            if(b1 == null) b1 = b0;
            // reorganizer -> shared editor
            yield return Frames(4);
            UICore.EnterReorganize();
            yield return Frames(2);
            h = ReorganizeHandle.FindLayout(id);
            if(h != null) {
                h.Target.position += new Vector3(-40f, -30f, 0f);
                ReorganizeHistory.Moved(h);
            }
            yield return Frames(2);
            UICore.ExitReorganize();
            yield return Session(2);
            Reapply(id);
            yield return Session(2);
            float[] b2 = t.GetBox();
            check($"layout {id}: a reorganizer move shows in the shared editor", Near(b2, b1[0] - 40f, b1[1] - 30f), Fmt(b1) + " -> " + Fmt(b2));
            // size, rotation, anchor and Closest, both ways (values, not positions)
            h = ReorganizeHandle.FindLayout(id);
            if(h == null) yield break;
            if(h.CanResize) {
                float tol = h.SizeFormat.EndsWith("px", StringComparison.Ordinal) ? 0.5f : 0.01f; // Panels and Practice round font sizes
                float s1 = h.MinSize + (h.MaxSize - h.MinSize) * 0.25f, s2 = h.MinSize + (h.MaxSize - h.MinSize) * 0.75f;
                int saves = Saves(h, () => t.SetScale(s1));
                check($"layout {id}: shared scale reaches the reorganizer", Mathf.Abs(h.GetSize() - s1) <= tol, $"{h.GetSize()} vs {s1}");
                check($"layout {id}: a shared resize is saved like the reorganizer's", saves == 1, $"saves={saves}");
                h.SetSize(s2);
                check($"layout {id}: reorganizer size reaches the shared editor", Mathf.Abs(t.GetScale() - s2) <= tol, $"{t.GetScale()} vs {s2}");
            }
            if(h.CanRotate) {
                int saves = Saves(h, () => t.SetRotation?.Invoke(30f));
                check($"layout {id}: shared rotation reaches the reorganizer", h.GetRotation() == 30f, $"{h.GetRotation()}");
                check($"layout {id}: a shared rotation is saved like the reorganizer's", saves == 1, $"saves={saves}");
                h.SetRotation(-15f);
                check($"layout {id}: reorganizer rotation reaches the shared editor", t.GetRotation?.Invoke() == -15f, $"{t.GetRotation?.Invoke()}");
            }
            if(t.SetAnchor != null) {
                yield return Session(2);
                float[] c = t.GetBox();
                KeyViewerSettings kv = KeyViewerOverlay.Conf;
                bool footHidden = id == LayoutIds.KeyViewer && MineById(LayoutIds.KeyViewerFoot)?.GetBox() == null;
                float footX = kv?.DmFootOffsetX ?? 0f, footY = kv?.DmFootOffsetY ?? 0f;
                t.SetAnchor(t.GetAnchor?.Invoke() == 0 ? 8 : 0);
                yield return Session(2);
                float[] c1 = t.GetBox();
                check($"layout {id}: a new anchor keeps the element in place", c != null && Near(c1, c[0], c[1]) && (int)h.CurrentAnchor == t.GetAnchor?.Invoke(),
                    $"{Fmt(c)} -> {Fmt(c1)} anchor {(int)h.CurrentAnchor}/{t.GetAnchor?.Invoke()}");
                // No foot keys: the foot is not built, so the anchor change leaves its offset alone and has it reseeded.
                if(footHidden && kv != null)
                    check("layout keyviewer: a hand anchor change leaves the hidden foot's offset alone and reseeds it",
                        kv.DmFootOffsetX == footX && kv.DmFootOffsetY == footY && !kv.DmFootPlaced,
                        $"({footX},{footY}) -> ({kv.DmFootOffsetX},{kv.DmFootOffsetY}) placed={kv.DmFootPlaced}");
            }
            if(t.SetAnchorClosest != null) {
                t.SetAnchorClosest(!closest0);
                check($"layout {id}: Closest reaches the reorganizer", h.AnchorClosest?.Invoke() == !closest0 && t.GetAnchorClosest?.Invoke() == !closest0, null);
            }
            // restore: Closest without its snap, rotation, scale, anchor, then the centre
            h.SetClosest?.Invoke(closest0);
            if(h.CanRotate) h.SetRotation(rot0);
            if(h.CanResize) h.SetSize(scale0);
            if(anchor0 >= 0 && h.SetAnchor != null && (int)h.CurrentAnchor != anchor0) h.SetAnchor((OverlayAnchor)anchor0);
            yield return Session(2);
            t.MoveTo(b0[0], b0[1]);
            yield return Session(2);
            Reapply(id);
            yield return Session(2);
            float[] b3 = t.GetBox();
            check($"layout {id}: restored", Near(b3, b0[0], b0[1]) && t.GetScale() == scale0 && (t.GetRotation?.Invoke() ?? 0f) == rot0
                && (t.GetAnchor?.Invoke() ?? -1) == anchor0 && (t.GetAnchorClosest?.Invoke() == true) == closest0,
                $"{Fmt(b0)} -> {Fmt(b3)} scale {t.GetScale()}/{scale0} rot {t.GetRotation?.Invoke()}/{rot0} anchor {t.GetAnchor?.Invoke()}/{anchor0}");
        } finally {
            unforce?.Invoke();
        }
    }
    // Edge cases: an element that cannot move right now, the editor's own lifecycle, a move just before a session ends.
    private static IEnumerator Edges(Action<string, bool, string> check, Action<string> info, List<string> seen) {
        if(MineById(LayoutIds.ProgressBar) is { } bar) {
            ProgressBarStyle style = ProgressBarOverlay.Conf.Style;
            ProgressBarOverlay.Conf.Style = ProgressBarStyle.Line;
            ProgressBarOverlay.Apply();
            yield return Session(2);
            check("layout: a line-style progress bar is hidden, not broken", bar.GetBox() == null, null);
            ProgressBarOverlay.Conf.Style = style;
            ProgressBarOverlay.Apply();
        } else info("layout: no progress bar to hide");
        yield return Frames(4);
        PrismBridge.OpenLayoutEditor();
        yield return Frames(3);
        check("layout: Edit layout opens the shared editor", UICore.SharedLayoutOpen, $"host={LayoutHost()}");
        PanelsOverlay.Conf.Panels.Add(new PanelConfig { Name = "selftest" });
        PanelsOverlay.Rebuild();
        yield return Frames(3);
        string extra = LayoutIds.Panel(PanelsOverlay.Conf.Panels.Count - 1);
        check("layout: a new panel becomes a target", MineById(extra) != null, extra);
        PanelsOverlay.Conf.Panels.RemoveAt(PanelsOverlay.Conf.Panels.Count - 1);
        PanelsOverlay.Rebuild();
        yield return Frames(3);
        check("layout: a removed panel drops out", MineById(extra) == null, extra);
        yield return PanelSteps(check, info);
        check("layout: nothing was reported as skipped", !seen.Any(l => l.Contains("skipped")), string.Join(" | ", seen));
        Ui.CloseTop();
        yield return Frames(4);
        check("layout: closing the editor ends the session", !UICore.SharedLayoutOpen, null);
        // The holder lets go (CamDisplay here, or nobody): Quartz's once-a-second retry takes the claim and hosts the editor itself.
        string holder = LayoutHost();
        object held = holder != null && holder != PrismBridge.Id ? HeldBy(holder) : null;
        try {
            if(held != null) ReleaseOwner(holder);
            float until = Time.unscaledTime + 1.5f;
            while(LayoutHost() != PrismBridge.Id && Time.unscaledTime < until) yield return null;
            PrismBridge.OpenLayoutEditor();
            yield return Frames(3);
            check("layout: Quartz hosts the shared editor once its holder lets go", LayoutHost() == PrismBridge.Id && QuartzLayout.Visible && UICore.SharedLayoutOpen,
                $"holder={holder} now={LayoutHost()} visible={QuartzLayout.Visible} session={UICore.SharedLayoutOpen}");
            Ui.CloseTop();
            yield return Frames(4);
            check("layout: and closing Quartz's editor ends the session", !QuartzLayout.Visible && !UICore.SharedLayoutOpen, null);
        } finally {
            if(held != null) check("layout: the holder gets its claims back", GiveBack(holder, held) && LayoutHost() == holder, $"holder={holder} now={LayoutHost()}");
        }
        yield return HiddenTakesNothing(check);
        EditTarget combo = MineById(LayoutIds.Combo);
        yield return Session(4); // a session just started: the overlay's gate turns its updater back on a frame later
        float[] c0 = combo?.GetBox();
        if(combo == null || c0 == null) {
            check("layout: a move right before the session ends is kept", false, "no combo box");
            yield break;
        }
        combo.MoveTo(c0[0] + 25f, c0[1] + 25f);
        yield return Frames(5); // no more reads: the session closes inside Grace
        yield return Session(4);
        ComboOverlay.Apply();
        yield return Session(2);
        float[] c1 = combo.GetBox();
        check("layout: a move right before the session ends is kept", Near(c1, c0[0] + 25f, c0[1] + 25f), Fmt(c0) + " -> " + Fmt(c1));
        combo.MoveTo(c0[0], c0[1]);
        yield return Session(2);
        yield return Frames(4);
    }
    // A panel's undo step follows its PanelConfig: through a reorder to the same panel, and to nothing once it is deleted.
    // Each step goes through the delegates captured before the list changed, as the editor's history does.
    private static IEnumerator PanelSteps(Action<string, bool, string> check, Action<string> info) {
        List<PanelConfig> panels = PanelsOverlay.Conf.Panels;
        if(panels.Count < 2) {
            info("layout: fewer than two panels, no reorder check");
            yield break;
        }
        yield return Session(2);
        EditTarget ta = MineById(LayoutIds.Panel(0)), tb = MineById(LayoutIds.Panel(1));
        float[] a0 = ta?.GetBox(), b0 = tb?.GetBox();
        float aSize = ta?.GetScale() ?? 0f, bSize = tb?.GetScale() ?? 0f;
        if(a0 != null && b0 != null) {
            tb!.SetScale(bSize < 40f ? bSize + 4f : bSize - 4f); // the gesture, on panel B
            tb.MoveTo(b0[0] + 40f, b0[1] + 30f);
            yield return Session(2);
            panels.Reverse(0, 2); // B is panel.0 now, A panel.1
            PanelsOverlay.Rebuild();
            yield return Session(3);
            tb.SetScale(bSize); // its undo
            tb.MoveTo(b0[0], b0[1]);
            yield return Session(2);
        }
        EditTarget nowB = MineById(LayoutIds.Panel(0)), nowA = MineById(LayoutIds.Panel(1));
        float[] b1 = nowB?.GetBox(), a1 = nowA?.GetBox();
        check("layout: an undo after a reorder restores the panel it was made on",
            a0 != null && b0 != null && Near(b1, b0[0], b0[1]) && nowB?.GetScale() == bSize && Near(a1, a0[0], a0[1]) && nowA?.GetScale() == aSize,
            $"B {Fmt(b0)} -> {Fmt(b1)} size {nowB?.GetScale()}/{bSize}; A {Fmt(a0)} -> {Fmt(a1)} size {nowA?.GetScale()}/{aSize}");
        if(a0 != null && b0 != null) {
            panels.Reverse(0, 2);
            PanelsOverlay.Rebuild();
        }
        panels.Insert(0, new PanelConfig { Name = "selftest" }); // first, so a positional id would land on a live panel
        PanelsOverlay.Rebuild();
        yield return Session(3);
        EditTarget tx = MineById(LayoutIds.Panel(0));
        float[] x0 = tx?.GetBox();
        if(x0 != null) tx!.MoveTo(x0[0] + 40f, x0[1] + 30f);
        yield return Session(2);
        panels.RemoveAt(0);
        PanelsOverlay.Rebuild();
        yield return Session(3);
        EditTarget first = MineById(LayoutIds.Panel(0));
        float[] f0 = first?.GetBox();
        float fSize = first?.GetScale() ?? 0f;
        if(x0 != null) {
            tx!.SetScale(fSize < 40f ? fSize + 4f : fSize - 4f); // the deleted panel's steps
            tx.MoveTo(x0[0], x0[1]);
        }
        yield return Session(2);
        float[] f1 = first?.GetBox();
        check("layout: a deleted panel's undo step does nothing",
            x0 != null && f0 != null && tx!.GetBox() == null && Near(f1, f0[0], f0[1]) && first?.GetScale() == fSize,
            $"deleted {Fmt(tx?.GetBox())}; panel.0 {Fmt(f0)} -> {Fmt(f1)} size {first?.GetScale()}/{fSize}");
    }
    // Outside a session Combo is hidden (no box): every write from the shared editor is refused, so a step replayed then
    // lands whole or not at all. Checked on the handle, not GetBox, which would start a session.
    private static IEnumerator HiddenTakesNothing(Action<string, bool, string> check) {
        yield return Frames(4);
        if(ComboOverlay.ConfMgr == null) { // Combo never turned on: no settings to write to
            check("layout: a hidden element takes no writes, so a replayed step lands whole or not at all", false, "combo off");
            yield break;
        }
        EditTarget t = MineById(LayoutIds.Combo);
        ComboSettings c = ComboOverlay.Conf;
        bool hidden = ReorganizeHandle.FindLayout(LayoutIds.Combo) is { LayoutShown: false };
        var before = (c.OffsetX, c.OffsetY, c.MasterSize, c.Rotation, c.Anchor, c.AnchorClosest);
        t?.SetAnchorClosest?.Invoke(!c.AnchorClosest);
        t?.SetAnchor?.Invoke(c.Anchor == 0 ? 8 : 0);
        t?.SetScale(c.MasterSize < 2f ? c.MasterSize + 0.5f : c.MasterSize - 0.5f);
        t?.SetRotation?.Invoke(c.Rotation + 20f);
        t?.MoveTo(10f, 10f);
        var after = (c.OffsetX, c.OffsetY, c.MasterSize, c.Rotation, c.Anchor, c.AnchorClosest);
        check("layout: a hidden element takes no writes, so a replayed step lands whole or not at all", t != null && hidden && before == after,
            $"hidden={hidden} {before} -> {after}");
        (c.OffsetX, c.OffsetY, c.MasterSize, c.Rotation, c.Anchor, c.AnchorClosest) = before;
        ComboOverlay.Apply();
    }
    // How often the handle's save hook (OnMoved, which the reorganizer runs after every edit) ran during the action.
    private static int Saves(ReorganizeHandle h, Action act) {
        Action saved = h.OnMoved;
        int n = 0;
        h.OnMoved = () => {
            n++;
            saved?.Invoke();
        };
        try { act(); } finally { h.OnMoved = saved; }
        return n;
    }
    // Every Conf field a layout target writes, kept raw and put back after the run, then saved (a file the run created is
    // deleted), so neither the run's moves nor a float round trip through the geometry reach the settings files. Every
    // field goes back before any element re-applies: Combo sits under the progress bar's band, and laid out against the
    // band's unrestored offset its RectTransform would read back (an arranging frame, Dispose) as a drifted offset.
    private static (Action Set, Action Finish)[] KeepSettings() => [
        Keep(ComboOverlay.ConfMgr, c => (c.OffsetX, c.OffsetY, c.MasterSize, c.Rotation, c.Anchor, c.AnchorClosest),
            (c, v) => (c.OffsetX, c.OffsetY, c.MasterSize, c.Rotation, c.Anchor, c.AnchorClosest) = v, ComboOverlay.Apply),
        Keep(JudgementOverlay.ConfMgr, c => (c.OffsetX, c.OffsetY, c.Size, c.Rotation, c.Anchor, c.AnchorClosest),
            (c, v) => (c.OffsetX, c.OffsetY, c.Size, c.Rotation, c.Anchor, c.AnchorClosest) = v, JudgementOverlay.Apply),
        Keep(SongTitleOverlay.ConfMgr, c => (c.OffsetX, c.OffsetY, c.MasterSize, c.Rotation, c.Anchor, c.AnchorClosest),
            (c, v) => (c.OffsetX, c.OffsetY, c.MasterSize, c.Rotation, c.Anchor, c.AnchorClosest) = v, SongTitleOverlay.Apply),
        Keep(ProgressBarOverlay.ConfMgr, c => (c.OffsetX, c.TopOffset, c.Rotation, c.Anchor, c.AnchorClosest, c.Style),
            (c, v) => (c.OffsetX, c.TopOffset, c.Rotation, c.Anchor, c.AnchorClosest, c.Style) = v, ProgressBarOverlay.Apply),
        Keep(PracticeDifficulty.ConfMgr, c => (c.OffsetX, c.OffsetY, c.FontSize, c.Rotation),
            (c, v) => (c.OffsetX, c.OffsetY, c.FontSize, c.Rotation) = v, PracticeOverlay.Apply),
        Keep(Calibration.ConfMgr, c => (c.PopupOffsetX, c.PopupOffsetY), (c, v) => (c.PopupOffsetX, c.PopupOffsetY) = v, null),
        Keep(KeyViewerOverlay.ConfMgr,
            c => (c.DmOffsetX, c.DmOffsetY, c.DmScale, c.DmRotation, c.DmAnchor, c.DmAnchorClosest, c.DmFootOffsetX, c.DmFootOffsetY,
                c.DmFootScale, c.DmFootRotation, c.DmFootPlaced, c.TabubOffsetX, c.TabubOffsetY, c.TabubScale, c.TabubRotation, c.TabubEnabled),
            (c, v) => (c.DmOffsetX, c.DmOffsetY, c.DmScale, c.DmRotation, c.DmAnchor, c.DmAnchorClosest, c.DmFootOffsetX, c.DmFootOffsetY,
                c.DmFootScale, c.DmFootRotation, c.DmFootPlaced, c.TabubOffsetX, c.TabubOffsetY, c.TabubScale, c.TabubRotation, c.TabubEnabled) = v,
            KeyViewerOverlay.Apply),
        Keep(PanelsOverlay.ConfMgr, c => (Order: c.Panels.ToList(), Fields: c.Panels.Select(p => (p.PosX, p.PosY, p.FontSize, p.Rotation, p.Anchor, p.AnchorClosest)).ToList()),
            static (c, v) => {
                if(!v.Order.SequenceEqual(c.Panels)) {
                    c.Panels.Clear();
                    c.Panels.AddRange(v.Order);
                    PanelsOverlay.Rebuild();
                }
                // A panel's position lives on its RectTransform (Rebuild and Dispose copy it back), so put it there too.
                for(int i = 0; i < v.Order.Count; i++) {
                    PanelConfig p = v.Order[i];
                    (p.PosX, p.PosY, p.FontSize, p.Rotation, p.Anchor, p.AnchorClosest) = v.Fields[i];
                    if(ReorganizeHandle.FindLayout(LayoutIds.Panel(i))?.Target is not { } rect) continue;
                    OverlayAnchors.Pin(rect, OverlayAnchors.Parse(p.Anchor));
                    rect.anchoredPosition = OverlayCalibration.Scale(new Vector2(p.PosX, p.PosY));
                }
            }, PanelsOverlay.Apply),
    ];
    private static (Action Set, Action Finish) Keep<TConf, TValue>(SettingsFile<TConf> file, Func<TConf, TValue> get, Action<TConf, TValue> set, Action apply)
        where TConf : class, ISettingsFile, new() {
        if(file?.Data is not { } conf) return default;
        TValue kept = get(conf);
        bool existed = File.Exists(file.Path);
        return (() => set(conf, kept), () => {
            apply?.Invoke();
            file.CancelPendingSave();
            if(existed) file.Save();
            else if(File.Exists(file.Path)) { // the run created it; a failed delete must not stop the other restores
                try { File.Delete(file.Path); } catch(Exception e) { MainCore.Log.Wrn($"prism self-test: could not delete {file.Path}: {e.Message}"); }
            }
        });
    }
    // Shows what a setting hides so it can be arranged; returns how to put the setting back.
    private static Action ForceShown(string id) {
        if(id == LayoutIds.ProgressBar && ProgressBarOverlay.Conf.Style == ProgressBarStyle.Line) {
            ProgressBarOverlay.Conf.Style = ProgressBarStyle.Modern;
            ProgressBarOverlay.Apply();
            return static () => {
                ProgressBarOverlay.Conf.Style = ProgressBarStyle.Line;
                ProgressBarOverlay.Apply();
            };
        }
        if(id == LayoutIds.Tabub && !KeyViewerOverlay.Conf.TabubEnabled) {
            KeyViewerOverlay.Conf.TabubEnabled = true;
            return static () => KeyViewerOverlay.Conf.TabubEnabled = false;
        }
        return null;
    }
    // Re-reads the element's settings, as a reload would.
    private static void Reapply(string id) {
        switch(id) {
            case LayoutIds.KeyViewer:
            case LayoutIds.KeyViewerFoot: KeyViewerOverlay.Apply(); break;
            case LayoutIds.Tabub: KeyViewerOverlay.ApplyTabub(); break;
            case LayoutIds.Combo: ComboOverlay.Apply(); break;
            case LayoutIds.Judgement: JudgementOverlay.Apply(); break;
            case LayoutIds.SongTitle: SongTitleOverlay.Apply(); break;
            case LayoutIds.ProgressBar: ProgressBarOverlay.Apply(); break;
            case LayoutIds.Practice: PracticeOverlay.Apply(); break;
            case LayoutIds.Calibration:
                CalibrationPopupUI.EndReorganize();
                CalibrationPopupUI.BeginReorganize();
                break;
            default: // panel.N: rebuilt without syncing that panel, so its position comes from its config
                int i = LayoutIds.Order(id) - LayoutIds.Order(LayoutIds.Panel(0));
                if(i >= 0 && i < PanelsOverlay.Conf.Panels.Count) PanelsOverlay.Rebuild(PanelsOverlay.Conf.Panels[i]);
                break;
        }
    }
    private static bool Near(float[] b, float x, float y) => b != null && Mathf.Abs(b[0] - x) <= 1.5f && Mathf.Abs(b[1] - y) <= 1.5f;
    private static string Fmt(float[] b) => b == null ? "null" : $"({b[0]:0.0},{b[1]:0.0} {b[2]:0.0}x{b[3]:0.0})";
    private static EditTarget MineById(string id) => Mine().FirstOrDefault(t => t.Id == id);
    // Quartz's targets as another mod's editor gets them: from PrismLib's registry, converted the way a host converts.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<EditTarget> Mine() {
        var list = new List<EditTarget>();
        foreach(KeyValuePair<string, PrismLib.LayoutTarget> kv in PrismLib.Layout.All())
            if(kv.Key == PrismBridge.Id) list.Add(QuartzLayout.ToEditTarget(kv.Key, kv.Value));
        return list;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string LayoutHost() => PrismLib.Claims.OwnerOf(PrismLib.StateKey.LayoutEditor);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object HeldBy(string owner) => PrismLib.Claims.Held.Where(kv => kv.Value == owner).Select(kv => kv.Key).ToList();
    // Hands back what ReleaseOwner took from a holder, first letting go of any of those keys Quartz took meanwhile (the debug
    // window's or the layout editor's): the run leaves no trace.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool GiveBack(string owner, object keys) {
        var held = (List<PrismLib.StateKey>)keys;
        if(held.Contains(PrismLib.StateKey.DebugPanel) && DebugOwner() == PrismBridge.Id) PrismBridge.ReleaseDebugClaimImpl();
        if(held.Contains(PrismLib.StateKey.LayoutEditor) && LayoutHost() == PrismBridge.Id) PrismBridge.ReleaseLayoutClaimImpl();
        PrismLib.ModHandle mod = PrismLib.Prism.Register(owner, null);
        bool all = true;
        foreach(PrismLib.StateKey k in held) all &= mod.ClaimState(k) != null;
        return all;
    }
}
#endif

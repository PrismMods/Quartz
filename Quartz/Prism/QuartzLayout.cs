#if !IL2CPP && !QUARTZ_KEYVIEWER
using System.Runtime.CompilerServices;
using PrismLib.UI.Toolkit;
using Quartz.Overlay;
using Quartz.UI.Utility;
using UnityEngine;
using PrismTheme = PrismLib.UI.Toolkit.Theme;
namespace Quartz.Prism;
/// <summary>
/// Every element the reorganizer moves, as a target for PrismLib's shared layout editor, and that editor when Quartz hosts
/// it (it holds the LayoutEditor claim, or PrismLib is absent). PrismLib.UI types may sit in fields here, so this class loads
/// only after PrismBridge.UiAvailable; PrismLib.dll types stay in the NoInlining helpers at the end, reached after Available.
/// </summary>
internal static class QuartzLayout {
    private static LayoutEditor editor;
    private static bool editorAll; // the editor shows every mod's targets, not only Quartz's
    private static int reloaded = -1;
    /// <summary>Quartz draws the editor: it holds the claim, or there is no PrismLib to hold one.</summary>
    internal static bool OwnsEditor => !PrismBridge.Available || PrismBridge.LayoutClaimed;
    internal static bool Visible => editor != null && editor.Visible;
    /// <summary>
    /// One target per tagged handle. Every delegate finds its handle on each call, so a rebuilt overlay keeps working: by id,
    /// or by owner for a panel (its PanelConfig), whose id moves with its place in the list.
    /// </summary>
    internal static IEnumerable<EditTarget> Targets() {
        var list = new List<EditTarget>();
        foreach(ReorganizeHandle h in ReorganizeHandle.LayoutHandles()) list.Add(Target(h));
        return list;
    }
    // Positions in screen px, rotation in degrees counter-clockwise (both as Quartz stores them), OverlayAnchor's order is
    // PrismLib's 0-8; the scale is the handle's own size (font px for Panels and Practice). No crop.
    private static EditTarget Target(ReorganizeHandle h) {
        string id = h.LayoutId;
        object owner = h.LayoutOwner; // a Quartz object (PanelConfig), never a PrismLib one
        ReorganizeHandle Find() => ReorganizeHandle.FindLayout(id, owner);
        // An element with no box (hidden, or not movable right now) takes no writes: a step the editor replays after the
        // session (an undo from any Facet window) lands whole or not at all.
        ReorganizeHandle Live() => Find() is { LayoutShown: true } r ? r : null;
        var t = new EditTarget {
            Owner = PrismBridge.Id, Id = id, Label = h.GetName?.Invoke() ?? id,
            GetBox = () => {
                PrismBridge.TouchLayout(); // the editor reads every box each frame it is open: this is how Quartz knows
                return Find()?.LayoutBox();
            },
            MoveTo = (x, y) => Live()?.LayoutMoveTo(x, y),
            GetScale = () => Find() is { CanResize: true } r ? r.GetSize() : 1f,
            SetScale = v => {
                if(Live() is not { CanResize: true } r) return;
                r.SetSize(Mathf.Clamp(v, r.MinSize, r.MaxSize));
                r.OnMoved?.Invoke(); // saves, as the reorganizer's ReorganizeHistory.Moved does after every edit
            },
            MinScale = h.CanResize ? h.MinSize : 1f,
            MaxScale = h.CanResize ? h.MaxSize : 1f,
        };
        if(h.CanRotate) {
            t.GetRotation = () => Find() is { CanRotate: true } r ? r.GetRotation() : 0f;
            t.SetRotation = v => {
                if(Live() is not { CanRotate: true } r) return;
                r.SetRotation(v);
                r.OnMoved?.Invoke();
            };
        }
        if(h.SetAnchor != null) {
            t.GetAnchor = () => Find() is { HasAnchor: true } r ? (int)r.CurrentAnchor : (int)OverlayAnchor.MiddleCenter;
            t.SetAnchor = a => Live()?.SetAnchor?.Invoke(OverlayAnchors.Parse(a)); // Quartz's rebase keeps the on-screen spot
        }
        if(h.AnchorClosest != null && h.SetClosest != null) {
            t.GetAnchorClosest = () => Find()?.AnchorClosest?.Invoke() == true;
            t.SetAnchorClosest = v => {
                if(Live() is not { } r) return;
                r.SetClosest?.Invoke(v);
                if(v) Reorganizer.SnapAnchor(r);
            };
        }
        return t;
    }
    /// <summary>Opens the editor here: every mod's targets when PrismLib is there, Quartz's own when it is not.</summary>
    internal static void Open(bool all) {
        if(editor == null || editorAll != all) {
            editor?.Dispose();
            editorAll = all;
            PrismWindows.Wire();
            using(PrismTheme.Use(PrismWindows.Scheme())) editor = all ? new LayoutEditor(AllTargets) : new LayoutEditor(Targets);
        }
        editor.Visible = true;
    }
    /// <summary>A mod's target list changed (PrismLib's Layout.Changed).</summary>
    internal static void Reload() {
        if(Visible) editor.Reload();
    }
    internal static void Tick() {
        if(editor == null) return;
        // Quartz's own list has no Layout.Changed to follow: a handle tagged or destroyed while open shows on the next frame.
        if(!editorAll && editor.Visible && reloaded != ReorganizeHandle.LayoutVersion) {
            reloaded = ReorganizeHandle.LayoutVersion;
            editor.Reload();
        }
        editor.Tick();
    }
    internal static void Shutdown() {
        editor?.Dispose();
        editor = null;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<EditTarget> AllTargets() {
        var list = new List<EditTarget>();
        foreach(KeyValuePair<string, PrismLib.LayoutTarget> kv in PrismLib.Layout.All()) list.Add(ToEditTarget(kv.Key, kv.Value));
        return list;
    }
    /// <summary>Quartz's targets for PrismLib's registry (DescribeLayout replaces the list).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static List<PrismLib.LayoutTarget> LayoutTargets() {
        var list = new List<PrismLib.LayoutTarget>();
        foreach(EditTarget e in Targets()) list.Add(ToLayoutTarget(e));
        return list;
    }
    // Field for field (CamDisplay's shape): only the delegates cross, never a PrismLib object inside one.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static PrismLib.LayoutTarget ToLayoutTarget(EditTarget e) => new() {
        Id = e.Id, Label = e.Label, GetBox = e.GetBox, MoveTo = e.MoveTo,
        GetScale = e.GetScale, SetScale = e.SetScale, MinScale = e.MinScale, MaxScale = e.MaxScale,
        GetRotation = e.GetRotation, SetRotation = e.SetRotation,
        GetAnchor = e.GetAnchor, SetAnchor = e.SetAnchor,
        GetAnchorClosest = e.GetAnchorClosest, SetAnchorClosest = e.SetAnchorClosest,
        GetCrop = e.GetCrop, SetCrop = e.SetCrop,
    };
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static EditTarget ToEditTarget(string owner, PrismLib.LayoutTarget t) => new() {
        Owner = owner, Id = t.Id, Label = t.Label, GetBox = t.GetBox, MoveTo = t.MoveTo,
        GetScale = t.GetScale, SetScale = t.SetScale, MinScale = t.MinScale, MaxScale = t.MaxScale,
        GetRotation = t.GetRotation, SetRotation = t.SetRotation,
        GetAnchor = t.GetAnchor, SetAnchor = t.SetAnchor,
        GetAnchorClosest = t.GetAnchorClosest, SetAnchorClosest = t.SetAnchorClosest,
        GetCrop = t.GetCrop, SetCrop = t.SetCrop,
    };
}
#endif

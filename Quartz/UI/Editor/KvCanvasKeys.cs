using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Quartz.UI.Editor;
internal sealed partial class KvCanvas {
    private const float NudgeRepeatDelay = 0.4f;
    private const float NudgeRepeatInterval = 0.05f;
    /// <summary>Static so a copy survives rebinding and can be pasted onto another tab.</summary>
    private static readonly List<KvElement> clipboard = [];
    private static readonly (KeyCode key, int dx, int dy)[] Arrows = [
        (KeyCode.LeftArrow, -1, 0),
        (KeyCode.RightArrow, 1, 0),
        (KeyCode.UpArrow, 0, -1),
        (KeyCode.DownArrow, 0, 1),
    ];
    private static readonly KeyCode[] ZoomInKeys = [KeyCode.Equals, KeyCode.Plus, KeyCode.KeypadPlus];
    private static readonly KeyCode[] ZoomOutKeys = [KeyCode.Minus, KeyCode.KeypadMinus];
    private static readonly KeyCode[] ZoomResetKeys = [KeyCode.Alpha0, KeyCode.Keypad0];
    private GameObject lastSelectedGo;
    private TMP_InputField lastSelectedInput;
    private KeyCode nudgeKey = KeyCode.None;
    private float nudgeNextAt;
    private float nudgeSnapshotAt = float.NegativeInfinity;
    private bool TextInputFocused() {
        GameObject go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if(go != lastSelectedGo) {
            lastSelectedGo = go;
            lastSelectedInput = go != null ? go.GetComponent<TMP_InputField>() : null;
        }
        return lastSelectedInput != null && lastSelectedInput.isFocused;
    }
    private void HandleKeyboard() {
        if(doc == null || TextInputFocused() || (InputSuppressed?.Invoke() ?? false)) return;
        if(CtrlOrCmdHeld()) {
            if(Input.GetKeyDown(KeyCode.Z)) {
                if(ShiftHeld()) Redo();
                else Undo();
                return;
            }
            if(Input.GetKeyDown(KeyCode.C)) {
                Copy();
                return;
            }
            if(Input.GetKeyDown(KeyCode.V)) {
                Paste();
                return;
            }
        }
        if(Input.GetKeyDown(KeyCode.Escape)) {
            ClearSelection();
            return;
        }
        if(Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) {
            DeleteSelection();
            return;
        }
        if(Input.GetKeyDown(KeyCode.RightBracket)) {
            NudgeZ(true);
            return;
        }
        if(Input.GetKeyDown(KeyCode.LeftBracket)) {
            NudgeZ(false);
            return;
        }
        HandleNudge();
    }
    /// <summary>
    /// Zoom on the bare +/-/0 keys. This is the keyboard's half of keeping zoom reachable now that
    /// the wheel pans: a ctrl-based binding would not exist on macOS, where the modifier cannot be
    /// read at all.
    ///
    /// Each direction lists several codes because which one arrives is not knowable here: `+` is
    /// shifted `=` on most layouts and its own key on a keypad, and the shift state that decides
    /// between them is exactly what this surface cannot see.
    /// </summary>
    private void HandleZoomKeys() {
        if(doc == null || TextInputFocused() || (InputSuppressed?.Invoke() ?? false)) return;
        if(AnyKeyDown(ZoomInKeys)) {
            ZoomBy(true);
            return;
        }
        if(AnyKeyDown(ZoomOutKeys)) {
            ZoomBy(false);
            return;
        }
        if(AnyKeyDown(ZoomResetKeys)) ResetView();
    }
    private static bool AnyKeyDown(KeyCode[] keys) {
        foreach(KeyCode key in keys)
            if(Input.GetKeyDown(key)) return true;
        return false;
    }
    private void HandleNudge() {
        if(selection.Count == 0) {
            nudgeKey = KeyCode.None;
            return;
        }
        foreach((KeyCode key, int dx, int dy) in Arrows) {
            if(!Input.GetKeyDown(key)) continue;
            nudgeKey = key;
            nudgeNextAt = Time.unscaledTime + NudgeRepeatDelay;
            ApplyNudge(dx, dy);
            return;
        }
        if(nudgeKey == KeyCode.None) return;
        if(!Input.GetKey(nudgeKey)) {
            nudgeKey = KeyCode.None;
            nudgeSnapshotAt = float.NegativeInfinity;
            history.EndNudge();
            return;
        }
        float now = Time.unscaledTime;
        if(now < nudgeNextAt) return;
        nudgeNextAt = now + NudgeRepeatInterval;
        foreach((KeyCode key, int dx, int dy) in Arrows) {
            if(key != nudgeKey) continue;
            ApplyNudge(dx, dy);
            return;
        }
    }
    /// <summary>Nudges move by one unit, not one grid step, so an element can be placed off-grid.</summary>
    private void ApplyNudge(int dx, int dy) {
        float now = Time.unscaledTime;
        // PushNudge throws the snapshot away inside its coalescing window, so serializing the
        // document on every repeat would be pure waste. Attempting at half the window is always
        // more often than the model can accept, so no push is ever missed.
        if(now - nudgeSnapshotAt >= KvHistory.NudgeCoalesceSeconds * 0.5f) {
            nudgeSnapshotAt = now;
            try {
                history.PushNudge(doc.ToJson(), now);
            } catch(Exception e) {
                MainCore.Log.Wrn($"[KvCanvas] nudge snapshot failed: {e.Message}");
            }
        }
        foreach(KvElement el in selection) el.MoveTo(el.X + dx, el.Y + dy);
        SyncGeometry();
        Mutated();
    }
    internal void DeleteSelection() {
        if(selection.Count == 0) return;
        PushHistory();
        foreach(KvElement el in selection) doc.Remove(tab, el);
        selection.Clear();
        doc.ReindexZOrder(tab);
        Rebuild();
        SelectionChanged?.Invoke();
        Mutated();
    }
    private void Copy() {
        if(selection.Count == 0) return;
        clipboard.Clear();
        foreach(KvElement el in doc.AllElements(tab))
            if(selection.Contains(el)) clipboard.Add(el.Clone());
    }
    private void Paste() {
        if(clipboard.Count == 0 || doc == null || string.IsNullOrEmpty(tab)) return;
        PushHistory();
        float top = 0f;
        foreach(Visual v in visuals) top = Mathf.Max(top, v.El.Z);
        List<KvElement> pasted = [];
        foreach(KvElement src in clipboard) {
            KvElement copy = src.Clone();
            copy.MoveTo(copy.X + KvSnap.PasteOffset, copy.Y + KvSnap.PasteOffset);
            copy.Z = ++top;
            doc.Add(tab, copy);
            pasted.Add(copy);
        }
        doc.ReindexZOrder(tab);
        Rebuild();
        SetSelection(pasted);
        Mutated();
    }
    internal void NudgeZ(bool forward) {
        if(selection.Count == 0 || doc == null) return;
        PushHistory();
        List<KvElement> all = doc.AllElements(tab);
        if(forward) {
            for(int i = all.Count - 2; i >= 0; i--)
                if(selection.Contains(all[i]) && !selection.Contains(all[i + 1]))
                    (all[i], all[i + 1]) = (all[i + 1], all[i]);
        } else {
            for(int i = 1; i < all.Count; i++)
                if(selection.Contains(all[i]) && !selection.Contains(all[i - 1]))
                    (all[i], all[i - 1]) = (all[i - 1], all[i]);
        }
        for(int i = 0; i < all.Count; i++) all[i].Z = i;
        Rebuild();
        Mutated();
    }
    internal void Undo() {
        if(doc == null) return;
        string current = Snapshot();
        if(current == null) return;
        Restore(history.Undo(current));
    }
    internal void Redo() {
        if(doc == null) return;
        string current = Snapshot();
        if(current == null) return;
        Restore(history.Redo(current));
    }
    private string Snapshot() {
        try {
            return doc.ToJson();
        } catch(Exception e) {
            MainCore.Log.Wrn($"[KvCanvas] snapshot failed: {e.Message}");
            return null;
        }
    }
    /// <summary>Adopts a reparsed snapshot in place. Deliberately not routed through
    /// <see cref="Bind"/>, which would clear the history it was just driven by.</summary>
    private void Restore(string snapshot) {
        if(snapshot == null) return;
        KvDocument restored;
        try {
            restored = KvDocument.Parse(snapshot);
        } catch(Exception e) {
            MainCore.Log.Err($"[KvCanvas] undo snapshot did not parse: {e}");
            return;
        }
        doc = restored;
        selection.Clear();
        Rebuild();
        DocumentReplaced?.Invoke(restored);
        SelectionChanged?.Invoke();
        Mutated();
    }
}

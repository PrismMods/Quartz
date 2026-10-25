using Quartz.Core;
using Quartz.Features.KeyViewer.Layout;
using Quartz.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Quartz.Features.KeyViewer.Editor;
internal sealed partial class KvCanvas {
    private const float NudgeRepeatDelay = 0.4f;
    private const float NudgeRepeatInterval = 0.05f;
    private static readonly List<KvElement> clipboard = [];
    private static Newtonsoft.Json.Linq.JObject styleClipboard;
    private static KvElementKind styleClipboardKind;
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
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if(shift && Input.GetKeyDown(KeyCode.C)) {
                CopyStyle();
                return;
            }
            if(shift && Input.GetKeyDown(KeyCode.V)) {
                PasteStyle();
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
        if(Input.GetKeyDown(KeyCode.Escape) && selection.Count > 0) {
            UICore.ConsumeEscape();
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
    private void ApplyNudge(int dx, int dy) {
        float now = Time.unscaledTime;
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
    internal bool HasStyleClipboard => styleClipboard != null;
    internal void CopyStyle() {
        if(selection.Count == 0) return;
        styleClipboard = selection[0].CopyStyle();
        styleClipboardKind = selection[0].Kind;
        StyleClipboardChanged?.Invoke();
    }
    // Keys and stats draw the same box, so they share looks; graphs and knobs only take their own kind's.
    private static bool StyleFits(KvElementKind kind) =>
        kind == styleClipboardKind
        || (kind is KvElementKind.Key or KvElementKind.Stat && styleClipboardKind is KvElementKind.Key or KvElementKind.Stat);
    internal void PasteStyle() {
        if(styleClipboard == null || doc == null || selection.Count == 0) return;
        bool any = false;
        foreach(KvElement el in selection) any |= StyleFits(el.Kind);
        if(!any) return;
        PushHistory();
        foreach(KvElement el in selection)
            if(StyleFits(el.Kind)) el.PasteStyle(styleClipboard);
        Rebuild();
        SetSelection([.. selection]);
        Mutated();
    }
    internal static event Action StyleClipboardChanged;
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
    // Driven by UndoTimeline (Ctrl+Z lives in UICore); false tells it this step is stale.
    internal bool Undo() {
        if(root == null || doc == null || !history.CanUndo) return false;
        string current = Snapshot();
        if(current == null) return false;
        string snapshot = history.Undo(current);
        if(snapshot == null) return false;
        Restore(snapshot);
        return true;
    }
    internal bool Redo() {
        if(root == null || doc == null || !history.CanRedo) return false;
        string current = Snapshot();
        if(current == null) return false;
        Restore(history.Redo(current));
        return true;
    }
    private string Snapshot() {
        try {
            return doc.ToJson();
        } catch(Exception e) {
            MainCore.Log.Wrn($"[KvCanvas] snapshot failed: {e.Message}");
            return null;
        }
    }
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
        if(!doc.HasTab(tab)) tab = doc.SelectedTab;
        selection.Clear();
        Rebuild();
        DocumentReplaced?.Invoke(restored);
        SelectionChanged?.Invoke();
        Mutated();
    }
}

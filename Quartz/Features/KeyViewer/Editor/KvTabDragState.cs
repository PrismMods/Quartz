namespace Quartz.UI.Editor;

internal sealed class KvTabDragState {
    private string tab;
    private int from = -1;
    private bool suppressPick;
    internal bool Reordering => tab != null;
    internal string Tab => tab;
    internal int From => from;

    internal void PointerDown(bool primary) {
        if(!primary) return;
        tab = null;
        from = -1;
        suppressPick = false;
    }

    internal bool Begin(string candidate, int index, int count, bool primary) {
        if(primary) suppressPick = true;
        if(!primary || string.IsNullOrEmpty(candidate) || count < 2 || index < 0) return false;
        tab = candidate;
        from = index;
        return true;
    }

    internal bool TryPick(bool primary) {
        if(!primary) return false;
        bool pick = !suppressPick;
        suppressPick = false;
        return pick;
    }

    internal bool End(out string draggedTab, out int originalIndex) {
        draggedTab = tab;
        originalIndex = from;
        tab = null;
        from = -1;
        return draggedTab != null;
    }

    internal void Reset() {
        tab = null;
        from = -1;
        suppressPick = false;
    }
}

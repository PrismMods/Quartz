namespace Quartz.Modules;
internal sealed class ModulePatchGate {
    private bool wanted;
    private bool applied;
    private bool sourceKnown;
    internal bool Applied => applied;
    internal bool Wanted => wanted;
    internal bool SourceArrived() {
        sourceKnown = true;
        return TryTake();
    }
    internal bool Want() {
        wanted = true;
        return TryTake();
    }
    private bool TryTake() {
        if(!wanted || applied || !sourceKnown) return false;
        applied = true;
        return true;
    }
    internal bool Release() {
        wanted = false;
        if(!applied) return false;
        applied = false;
        return true;
    }
    internal void Forget() {
        Release();
        sourceKnown = false;
    }
}

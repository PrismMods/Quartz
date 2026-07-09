namespace Quartz.Core;
public sealed class FeatureRegistry {
    private readonly List<(string Name, Action Step)> enableSteps = [];
    private readonly List<(string Name, Action Step)> disableSteps = [];
    public void OnEnable(string name, Action step) => enableSteps.Add((name, step));
    public void OnDisable(string name, Action step) => disableSteps.Add((name, step));
    public void Register(string name, Action onEnable, Action onDisable) {
        if(onEnable != null) enableSteps.Add((name, onEnable));
        if(onDisable != null) disableSteps.Add((name, onDisable));
    }
    public void EnableAll() {
        foreach((_, Action step) in enableSteps) step();
    }
    public void DisableAll() {
        foreach((_, Action step) in disableSteps) step();
    }
}

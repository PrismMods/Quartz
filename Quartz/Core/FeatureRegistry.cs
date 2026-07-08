namespace Quartz.Core;

// Centralizes the per-feature enable/disable wiring that QuartzRuntime used to
// hard-code as two long flat blocks inside SetModEnabled. Each feature registers
// its enable and/or disable step here in Initialize(); SetModEnabled then just
// calls EnableAll()/DisableAll().
//
// Enable and disable are two independent ordered lists because the original
// sequences are NOT symmetric and do NOT run in the same order:
//  - the overlays disable in REVERSE of their enable order (SongTitleOverlay
//    disposes first, PanelsOverlay last), while the non-overlay features
//    disable in FORWARD order;
//  - disable has teardown-only steps (UiHider, EditorFeature, AutoDeafen) with
//    no enable counterpart.
// So each step is registered separately via OnEnable / OnDisable, preserving
// the exact order SetModEnabled used to run them in. Behaviour is identical —
// the wiring just lives in one place now, and a new feature is one or two
// registration lines instead of editing two interleaved blocks.
//
// The OnModEnabledChanged broadcast is NOT part of the registry: it is a single
// event fired between the two passes from SetModEnabled, not a per-feature step.
public sealed class FeatureRegistry {
    private readonly List<(string Name, Action Step)> enableSteps = [];
    private readonly List<(string Name, Action Step)> disableSteps = [];

    public void OnEnable(string name, Action step) => enableSteps.Add((name, step));
    public void OnDisable(string name, Action step) => disableSteps.Add((name, step));

    // Convenience for the common case where a feature's enable and disable share
    // an ordering slot. Prefer OnEnable/OnDisable directly when the two sequences
    // need different positions (see the overlays above).
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

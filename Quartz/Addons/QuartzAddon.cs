namespace Quartz.Addons;

// Base class every addon implements. Build a .qaddon (or plain .dll) against
// sdk/QuartzAddon.dll with one non-abstract subclass, drop it into
// UserData/Quartz/Addons, and Quartz loads it and drives the lifecycle below.
// See sdk/README.md and the AddonExample project for a worked example of
// stats, tabs, settings, events and Harmony patches.
public abstract class QuartzAddon {
    // Stable identity. Defaults to the file/folder-derived id assigned at
    // discovery; override to pin it (used for the settings file name, the
    // Harmony id and the enabled-state key, so keep it filename-safe).
    public virtual string Id => GetType().Name;
    public virtual string Name => Id;
    public virtual string Version => "1.0.0";
    public virtual string Author => "";

    // Per-addon services (logger, Harmony, settings, stat/tab registration).
    // Assigned before OnLoad; never null inside the lifecycle callbacks.
    public AddonContext Context { get; internal set; }

    // Once, right after instantiation (mod may still be disabled). Register
    // stats/tabs/settings here — registrations are only picked up when the
    // pages/panels (re)build, and a reload rebuilds both.
    public virtual void OnLoad() { }

    // Mod master-switch turned on while this addon is enabled (also fires on
    // load when the mod is already on). Pair with OnDisable.
    public virtual void OnEnable() { }

    // Mod master-switch turned off, addon toggled off, or teardown started.
    public virtual void OnDisable() { }

    // Every frame while the mod is on and the addon is enabled. Keep it cheap.
    public virtual void OnTick() { }

    // Final teardown (addon reload, mod unload or game quit). Undo anything
    // OnLoad did outside Context — Context-owned resources (Harmony patches,
    // stats, tabs, settings) are cleaned up automatically after this returns.
    public virtual void OnUnload() { }
}

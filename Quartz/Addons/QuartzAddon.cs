namespace Quartz.Addons;
public abstract class QuartzAddon {
    public virtual string Id => GetType().Name;
    public virtual string Name => Id;
    public virtual string Version => "1.0.0";
    public virtual string Author => "";
    public AddonContext Context { get; internal set; }
    public virtual void OnLoad() { }
    public virtual void OnEnable() { }
    public virtual void OnDisable() { }
    public virtual void OnTick() { }
    public virtual void OnUnload() { }
}

namespace Quartz.Modules;
public abstract class QuartzModule {
    public ModuleContext Context { get; internal set; }
    public virtual void OnLoad() { }
    public virtual void OnEnable() { }
    public virtual void OnDisable() { }
    public virtual void OnTick() { }
    public virtual void OnUnload() { }
}
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class QuartzModuleInfoAttribute(string id, string version, int coreAbi) : Attribute {
    public string Id { get; } = id;
    public string Version { get; } = version;
    public int CoreAbi { get; } = coreAbi;
}

namespace Quartz.Features;
public abstract class QuartzFeature {
    public FeatureContext Context { get; internal set; }
    public virtual void OnLoad() { }
    public virtual void OnEnable() { }
    public virtual void OnDisable() { }
    public virtual void OnTick() { }
    public virtual void OnUnload() { }
}
/// <summary>Marks a built-in feature's entry class; FeatureService finds it by reflection.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FeatureAttribute(string id, string group, int order, string name, string nameKey) : Attribute {
    public string Id { get; } = id;
    public string Group { get; } = group;
    public int Order { get; } = order;
    public string Name { get; } = name;
    public string NameKey { get; } = nameKey;
    public string[] Deps { get; set; } = [];
    // Namespaces whose [HarmonyPatch] classes this feature owns; null means the entry class's namespace.
    public string[] Patches { get; set; }
}

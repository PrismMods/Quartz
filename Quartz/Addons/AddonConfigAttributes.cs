namespace Quartz.Addons;
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class SectionAttribute(string title) : Attribute {
    public string Title { get; } = title;
}
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class NameAttribute(string label) : Attribute {
    public string Label { get; } = label;
}
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class DescAttribute(string text) : Attribute {
    public string Text { get; } = text;
}
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class SliderAttribute(float min, float max, float step = 0f) : Attribute {
    public float Min { get; } = min;
    public float Max { get; } = max;
    public float Step { get; } = step;
}
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ChoicesAttribute(params string[] values) : Attribute {
    public string[] Values { get; } = values;
}
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ReloadRequiredAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class IgnoreAttribute : Attribute { }

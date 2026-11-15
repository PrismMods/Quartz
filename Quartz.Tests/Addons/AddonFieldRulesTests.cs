using Quartz.Addons;
using static Asserts;
static class AddonFieldRulesTests {
    public static void Run() {
        Assert(AddonFieldRules.Kind(typeof(bool), false, false) == AddonFieldKind.Toggle, "bool is a toggle");
        Assert(AddonFieldRules.Kind(typeof(DayOfWeek), false, false) == AddonFieldKind.Enum, "an enum is a dropdown");
        Assert(AddonFieldRules.Kind(typeof(string), false, true) == AddonFieldKind.Choices, "a string with choices is a dropdown");
        Assert(AddonFieldRules.Kind(typeof(string), false, false) == AddonFieldKind.Text, "a string with empty choices is text");
        Assert(AddonFieldRules.Kind(typeof(int), true, false) == AddonFieldKind.IntSlider, "an int with a slider");
        Assert(AddonFieldRules.Kind(typeof(double), true, false) == AddonFieldKind.Slider, "a double with a slider");
        Assert(AddonFieldRules.Kind(typeof(int?), true, false) == AddonFieldKind.NullableNumber, "a nullable number is never a slider");
        Assert(AddonFieldRules.Kind(typeof(float), false, false) == AddonFieldKind.Number, "a number without a slider is typed");
        Assert(AddonFieldRules.Kind(typeof(long), false, false) == AddonFieldKind.Unsupported, "only int, float and double are numbers");
        Assert(AddonFieldRules.Kind(typeof(bool?), false, false) == AddonFieldKind.Unsupported, "a nullable bool has no row");
        Assert(AddonFieldRules.Kind(typeof(DayOfWeek?), false, false) == AddonFieldKind.Unsupported, "a nullable enum has no row");
        Assert(AddonFieldRules.Kind(typeof(Dictionary<string, string>), false, false) == AddonFieldKind.Dict, "a string map");
        Assert(AddonFieldRules.Kind(typeof(List<KeyValuePair<string, string>>), false, false) == AddonFieldKind.Pairs, "a pair list");
        Assert(AddonFieldRules.Kind(typeof(Uri), false, false) == AddonFieldKind.Unsupported, "anything else has no row");
        Assert(AddonFieldRules.Label("MaxBpmLimit", null) == "Max Bpm Limit" && AddonFieldRules.Label("X", "Shown") == "Shown", "labels");
        Assert(AddonFieldRules.ParseNumber(" ", typeof(int?), out object v1) && v1 == null, "blank nullable is null");
        Assert(!AddonFieldRules.ParseNumber("", typeof(int), out _), "empty non-nullable fails");
        Assert(AddonFieldRules.ParseNumber("1.5", typeof(double), out object v2) && (double)v2 == 1.5, "invariant decimal");
        Assert(!AddonFieldRules.ParseNumber("1,5x", typeof(double), out _), "bad text fails");
        Assert(!AddonFieldRules.ParseNumber("NaN", typeof(int), out _), "NaN never reaches an int");
    }
}

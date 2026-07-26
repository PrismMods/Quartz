using Quartz.Modules;
using static Asserts;
static class ModuleOrderTests {
    private static ModuleManifest M(string id, int order, params string[] deps) =>
        new() { Id = id, Name = id, Order = order, CoreAbi = 1, Version = "1", Deps = deps };
    private static List<string> Ids(ModuleOrder.Result result) => result.Ordered.ConvertAll(m => m.Id);
    public static void TestDependenciesLoadBeforeDependents() {
        ModuleOrder.Result result = ModuleOrder.Sort([M("combo", 10, "progressbar"), M("progressbar", 20)]);
        Assert(Ids(result).IndexOf("progressbar") < Ids(result).IndexOf("combo"),
            "a dependency is ordered before its dependent even when its order is higher");
        Assert(result.Rejected.Count == 0, "nothing is rejected when the graph is satisfiable");
    }
    public static void TestOrderIsDeterministic() {
        List<ModuleManifest> input = [M("b", 10), M("a", 10), M("c", 5)];
        Assert(Ids(ModuleOrder.Sort(input)) is ["c", "a", "b"], "ties break on id, and order wins over id");
        Assert(Ids(ModuleOrder.Sort(input)) is ["c", "a", "b"], "sorting twice gives the same answer");
    }
    public static void TestMissingDependencyCascades() {
        ModuleOrder.Result result = ModuleOrder.Sort([M("combo", 10, "progressbar"), M("skin", 20, "combo")]);
        Assert(result.Ordered.Count == 0, "neither module loads");
        Assert(result.Rejected["combo"].Contains("progressbar"), "the direct cause names the missing module");
        Assert(result.Rejected["skin"].Contains("combo"), "the dependent is cascade-skipped, not silently dropped");
    }
    public static void TestCycleRejectsEveryMember() {
        ModuleOrder.Result result = ModuleOrder.Sort([M("a", 10, "b"), M("b", 20, "a"), M("c", 30)]);
        Assert(Ids(result) is ["c"], "modules outside the cycle still load");
        Assert(result.Rejected.ContainsKey("a") && result.Rejected.ContainsKey("b"), "every cycle member is rejected");
    }
}

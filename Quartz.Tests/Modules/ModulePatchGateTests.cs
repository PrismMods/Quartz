using Quartz.Modules;
using static Asserts;
static class ModulePatchGateTests {
    public static void TestPatchesWaitForBothTheSourceAndTheWant() {
        ModulePatchGate gate = new();
        Assert(!gate.SourceArrived(), "a declared assembly alone must not patch");
        Assert(!gate.Applied, "nothing is applied until the module is activated");
        Assert(gate.Want(), "activation after the declaration must patch");
        Assert(gate.Applied, "the gate now reports applied");
    }
    public static void TestActivationBeforeTheDeclarationStillPatches() {
        ModulePatchGate gate = new();
        Assert(!gate.Want(), "activation with no declared assembly yet must not patch");
        Assert(gate.Wanted, "but the want is remembered");
        Assert(gate.SourceArrived(), "the declaration arriving later must patch");
        Assert(gate.Applied, "the gate now reports applied");
    }
    public static void TestPatchingIsIdempotent() {
        ModulePatchGate gate = new();
        gate.SourceArrived();
        Assert(gate.Want(), "first activation patches");
        Assert(!gate.Want(), "a second activation must not patch again");
        Assert(!gate.SourceArrived(), "a second declaration must not patch again");
    }
    public static void TestReleaseOnlyReportsWorkWhenThereWasSome() {
        ModulePatchGate gate = new();
        Assert(!gate.Release(), "releasing a gate that never patched is a no-op");
        gate.SourceArrived();
        gate.Want();
        Assert(gate.Release(), "releasing an applied gate reports work to undo");
        Assert(!gate.Applied, "and clears applied");
        Assert(!gate.Release(), "releasing twice must not unpatch twice");
    }
    public static void TestReleaseClearsTheWantSoDisableStaysDisabled() {
        ModulePatchGate gate = new();
        gate.SourceArrived();
        gate.Want();
        gate.Release();
        Assert(!gate.Wanted, "release clears the want, so a stray declaration cannot re-patch");
        Assert(!gate.SourceArrived(), "a re-declaration while disabled must not patch");
        Assert(gate.Want(), "the next activation patches again");
    }
    public static void TestForgetRequiresTheAssemblyToBeDeclaredAgain() {
        ModulePatchGate gate = new();
        gate.SourceArrived();
        gate.Want();
        gate.Forget();
        Assert(!gate.Applied, "forget releases");
        Assert(!gate.Want(), "after a teardown, wanting alone must not patch a forgotten assembly");
        Assert(gate.SourceArrived(), "a reload declares the assembly again and patches");
    }
}

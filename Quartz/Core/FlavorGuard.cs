namespace Quartz.Core;
// Keeps a second Quartz build dormant when another loader already runs one in this process.
internal static class FlavorGuard {
    private const string OwnerSlot = "PrismMods.Quartz.ActiveBuild.Owner";
    private const string IdentitySlot = "PrismMods.Quartz.ActiveBuild.Identity";
    private static readonly string claimGate = string.Intern("PrismMods.Quartz.FlavorGuard");
    internal static object CreateLease() => new();
    internal static bool TryClaim(object lease, out string conflict) {
        if(lease == null) throw new ArgumentNullException(nameof(lease));
        string self = typeof(FlavorGuard).Assembly.GetName().Name;
        lock(claimGate) {
            object owner = AppDomain.CurrentDomain.GetData(OwnerSlot);
            if(owner != null && !ReferenceEquals(owner, lease)) {
                conflict = AppDomain.CurrentDomain.GetData(IdentitySlot) as string ?? "unknown Quartz build";
                return false;
            }
            // Publish the identity before the owner; both are read under claimGate.
            AppDomain.CurrentDomain.SetData(IdentitySlot, self);
            AppDomain.CurrentDomain.SetData(OwnerSlot, lease);
            conflict = null;
            return true;
        }
    }
    internal static void Release(object lease) {
        if(lease == null) throw new ArgumentNullException(nameof(lease));
        lock(claimGate) {
            if(!ReferenceEquals(AppDomain.CurrentDomain.GetData(OwnerSlot), lease)) return;
            AppDomain.CurrentDomain.SetData(OwnerSlot, null);
            AppDomain.CurrentDomain.SetData(IdentitySlot, null);
        }
    }
    internal static string Message(string conflict) =>
        $"Another Quartz build ({conflict}) is initializing or active — {Info.Name} is waiting dormant "
        + "to avoid a double menu.";
}

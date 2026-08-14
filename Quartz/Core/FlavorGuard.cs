namespace Quartz.Core;
/// <summary>
/// Keeps a second Quartz build dormant when another loader/flavour is already
/// active in the same process.
/// </summary>
/// <remarks>
/// Every build registers the same uGUI menu and keybind. Loader ordering is not
/// guaranteed, so the guard gives one loader an initialization lease while the
/// others wait. A failed initializer releases its lease and lets a waiter try.
/// </remarks>
internal static class FlavorGuard {
    private const string OwnerSlot = "PrismMods.Quartz.ActiveBuild.Owner";
    private const string IdentitySlot = "PrismMods.Quartz.ActiveBuild.Identity";
    private static readonly string claimGate = string.Intern("PrismMods.Quartz.FlavorGuard");
    /// <summary>Creates an opaque token owned by one loader instance.</summary>
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
            // Publish the diagnostic identity before the owner. Both reads happen
            // under claimGate, so a contender never observes a partial claim.
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

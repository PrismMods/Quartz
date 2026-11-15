#if !IL2CPP
namespace Quartz.UI.FacetUi;
internal static partial class QuartzShell {
#if DEBUG
    /// <summary>Set by a self-test: it hears each Do's name, and true skips the action. Null outside a self-test.</summary>
    internal static Func<string, bool> Doing;
#endif
    /// <summary>
    /// The one seam for a page button's side effect (a write, a delete, a launch, a dialog, the network), named
    /// "page.action[:arg]". run holds the whole effect, work it defers included, so a self-test can stop all of it.
    /// </summary>
    internal static void Do(string what, Action run) {
#if DEBUG
        Func<string, bool> doing = Doing;
        if(doing != null && doing(what)) return;
#endif
        run();
    }
}
#endif

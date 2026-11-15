#if DEBUG && !IL2CPP
using System.Collections;
using Quartz.Async;
using HandOff = Quartz.UI.MenuHandOff;
namespace Quartz.UI.FacetUi;
/// <summary>
/// What the non-form pages share: the seam every side effect runs through, and a failed launch's way back to the menu it
/// left.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string ReopenPage = "tuf.settings";

    private static IEnumerator NonForm(Action<string, bool, string> check) {
        bool wasOpen = UICore.IsOpen;
        if(wasOpen) UICore.Close(true);
        HandOff.ReturnTo = HandOff.Menu.None;
        try {
            yield return ReopenChecks(check);
            yield return DoChecks(check);
        } finally {
            HandOff.ReturnTo = HandOff.Menu.None;
            QuartzShell.Hide();
            if(UICore.IsOpen) UICore.Close(true);
            if(wasOpen) UICore.Open(true);
        }
        yield return Frames(2);
    }

    private static bool NewOn(string page) => FacetBridge.NewMenuVisible() && !UICore.IsOpen && QuartzShell.CurrentPage == page;
    private static bool ClassicOn() => UICore.IsOpen && !FacetBridge.NewMenuVisible();

    private static IEnumerator ShutBoth() {
        HandOff.ReturnTo = HandOff.Menu.None;
        QuartzShell.Hide();
        if(UICore.IsOpen) UICore.Close(true);
        yield return Frames(2);
    }

    private static IEnumerator ReopenChecks(Action<string, bool, string> check) {
        FacetBridge.OpenNewMenu(ReopenPage);
        yield return Frames(1);
        UICore.Dismiss();
        bool shut = !FacetBridge.NewMenuVisible() && !UICore.IsOpen;
        UICore.Reopen();
        yield return Frames(2);
        check("Reopen brings back the new menu Dismiss closed, on its page", shut && NewOn(ReopenPage), $"shut={shut} {Showing()}");
        yield return ShutBoth();

        UICore.Open(true);
        yield return Frames(1);
        UICore.Dismiss();
        UICore.Reopen();
        yield return Frames(2);
        check("Reopen brings back the classic menu Dismiss closed", ClassicOn(), Showing());
        yield return ShutBoth();

        FacetBridge.OpenNewMenu(ReopenPage);
        yield return Frames(1);
        UICore.Reopen();
        yield return Frames(2);
        check("Reopen with the new menu still open (no Dismiss) leaves it on its page and the classic menu shut", NewOn(ReopenPage), Showing());
        yield return ShutBoth();

        FacetBridge.OpenNewMenu(ReopenPage);
        yield return Frames(1);
        UICore.Dismiss();
        UICore.Dismiss();
        UICore.Reopen();
        yield return Frames(2);
        check("UICore.Reopen: a second Dismiss with nothing open keeps the new menu as the target", NewOn(ReopenPage), Showing());
        yield return ShutBoth();

        FacetBridge.OpenNewMenu(ReopenPage);
        yield return Frames(1);
        UICore.Dismiss();
        UICore.Open(true);
        yield return Frames(1);
        UICore.Close(true);
        yield return Frames(1);
        UICore.Reopen();
        yield return Frames(2);
        check("Reopen after an earlier Dismiss from the new menu and a later Open of the classic menu reopens the classic menu", ClassicOn(), Showing());
        yield return ShutBoth();
    }

    private static IEnumerator DoChecks(Action<string, bool, string> check) {
        int ran = 0;
        var names = new List<string>();
        Func<string, bool> was = QuartzShell.Doing;
        try {
            QuartzShell.Doing = null;
            QuartzShell.Do("selftest.plain", () => ran++);
            bool plain = ran == 1;
            QuartzShell.Doing = n => { names.Add(n); return true; };
            QuartzShell.Do("selftest.held", () => ran++);
            check("QuartzShell.Do runs its action unless a self-test intercepts it", plain && ran == 1 && names.SequenceEqual(["selftest.held"]),
                $"plain={plain} ran={ran} names={string.Join(",", names)}");
        } finally {
            QuartzShell.Doing = was;
        }
        int deferred = 0;
        yield return Recorded((n, ok, d) => check(n, ok && deferred == 0, $"{d} ran={deferred}"), "Recorded intercepts a Do the click deferred to the next pump",
            () => MainThread.Enqueue(() => QuartzShell.Do("selftest.deferred", () => deferred++)), "selftest.deferred");
    }

    /// <summary>Clicks with every Do recorded and skipped, through two main-thread pumps, and checks the click asked for exactly want.</summary>
    private static IEnumerator Recorded(Action<string, bool, string> check, string name, Action click, string want) {
        var names = new List<string>();
        Func<string, bool> was = QuartzShell.Doing;
        QuartzShell.Doing = n => { names.Add(n); return true; };
        try {
            click();
            yield return Frames(3);
        } finally {
            QuartzShell.Doing = was;
        }
        check(name, names.Count == 1 && names[0] == want, $"recorded={string.Join(",", names)} want={want}");
    }
}
#endif

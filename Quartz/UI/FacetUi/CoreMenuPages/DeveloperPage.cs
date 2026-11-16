#if !IL2CPP
using Facet;
using Quartz.Async;
using Quartz.Core;
using Quartz.UI.Utility;
using Quartz.Update;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.CoreMenuPages;
/// <summary>Developer in the new menu, row for row as PageDeveloper. The status is English, as on the classic page, and read
/// at build, on Refresh Status and, while the page shows, whenever the updater changes.</summary>
internal static class DeveloperPage {
    internal static void Build(VisualElement p) {
        QuartzShell.Header(p, "DEVELOPER", null);
        VisualElement card = Controls.Card(p, null);
        Label text = null;
        void Refresh() {
            Words.Label(text, Status(), own: true);
            QuartzShell.Surface?.Refresh();
        }

        VisualElement updater = ShellRows.Section(card, "SECTION_UPDATER", o => {
            o.Id = "SECTION_UPDATER";   // a Section names its header only by Id
            o.Open = true;
        });
        Toggle(updater, "DEV_SIM_UPDATE", "dev_sim_update", () => UpdateService.DevSimulate, v => QuartzShell.Do("developer.simulate:" + v, () => {
            UpdateService.SetDevSimulate(v);
        }));
        ShellRows.Buttons(updater, new ShellRows.Btn("DEV_CHECK", () => QuartzShell.Do("developer.check", UpdateService.Check)),
            new ShellRows.Btn("DEV_CLEAR_SKIP", () => {
                QuartzShell.Do("developer.clearskip", () => {
                    MainCore.Conf.SkippedVersion = "";
                    MainCore.ConfMgr.RequestSave();
                });
                Refresh();
            }), new ShellRows.Btn("DEV_REFRESH", Refresh));

        VisualElement seasonal = ShellRows.Section(card, "SECTION_SEASONAL", o => {
            o.Id = "SECTION_SEASONAL";
            o.Open = true;
        });
        Toggle(seasonal, "DEV_APRIL_FOOLS", "dev_april_fools", () => AprilFools.DevForced, v => QuartzShell.Do("developer.aprilfools:" + v, () => {
            AprilFools.SetDevForced(v);
            MainCore.Conf.AprilFoolsForced = v;
            MainCore.ConfMgr.RequestSave();
        }));

        VisualElement statusBody = ShellRows.Section(card, "SECTION_STATUS", o => {
            o.Id = "SECTION_STATUS";
            o.Open = true;
        });
        // Full width, as the classic page's text block; Info would hold it in its right-hand value column.
        text = Ui.Text(statusBody, Status(), own: true);
        text.name = "developer-status-input";
        text.style.whiteSpace = WhiteSpace.Normal;
        // Only while shown: the search index builds this page detached, and must not subscribe.
        Action changed = Refresh;
        Ui.WhileAttached(card, () => UpdateService.OnChanged += changed, () => UpdateService.OnChanged -= changed);
    }

    // A switch with a hotkey (a right-click sets or clears it). set holds the whole write, inside Do.
    private static void Toggle(VisualElement parent, string key, string classicId, Func<bool> get, Action<bool> set) {
        Bind<bool> bind = Bind.To(get, set);
        ShellRows.Flips(classicId, bind);
        Control<bool> c = null;
        c = Controls.Toggle(parent, key, bind, o => {
            o.Id = key;
            ShellRows.Hotkeys(o, classicId, () => c!.Root);
        });
    }

    // The classic page's status text, line for line.
    private static string Status() {
        UpdateInfo available = UpdateService.Available;
        string skipped = string.IsNullOrEmpty(MainCore.Conf.SkippedVersion) ? "none" : MainCore.Conf.SkippedVersion;
        return string.Join("\n", new[] {
            $"Version:         v{Info.DisplayVersion}",
            $"Channel:         {Info.ChannelKind}",
            $"Mod enabled:     {MainCore.IsModEnabled}",
            $"Update channel:  {MainCore.Conf.GetUpdateChannel()}",
            $"Update status:   {UpdateService.Status}",
            $"Failure:         {UpdateService.Failure}",
            $"Progress:        {(UpdateService.Progress < 0f ? "n/a" : UpdateService.Progress.ToString("P0"))}",
            $"Available:       {(available == null ? "none" : available.Tag)}",
            $"Skipped version: {skipped}",
            $"Simulate update: {UpdateService.DevSimulate}",
            $"April Fools:     {AprilFools.Active}{(AprilFools.DevForced ? " (forced)" : "")}",
            $"Repo:            {Info.RepoOwner}/{Info.RepoName}",
            $"Last message:    {UpdateService.Message}",
        });
    }
}
#endif

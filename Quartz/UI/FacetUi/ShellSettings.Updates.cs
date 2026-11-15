#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Update;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Settings' Updates card, row for row as the classic page's section: the channel, Check for updates, the status, and the
/// buttons each update state offers. Facet has no meter, so the download bar is its percentage. It follows UpdateService
/// only while shown.
/// </summary>
internal static partial class ShellSettings {
    private static readonly ReleaseChannel[] Channels = [ReleaseChannel.Stable, ReleaseChannel.Beta, ReleaseChannel.Alpha];
    // Why the last restart failed; any state but Installed forgets it.
    private static string restartError;

    private static bool UpdateReady => UpdateService.Status == UpdateStatus.Available && UpdateService.Available != null;

    internal static void UpdatesCard(VisualElement p) {
        VisualElement card = Card(p, "UPDATES");
        Controls.Dropdown(card, "UPDATE_CHANNEL_LABEL", QuartzShell.Writes(() => C.GetUpdateChannel(), ch => {
            C.UpdateChannel = (int)ch;
            Save();
            UpdateLaunchPrefs.Write();
        }), Channels, ch => "UPDATE_CHANNEL_" + ch.ToString().ToUpperInvariant(), o => {
            o.Id = "UPDATE_CHANNEL_LABEL";
            o.Tooltip = "DESC_UPDATE_CHANNEL";
        });
        Controls.Action(card, "", "CHECK_FOR_UPDATES", UpdateService.Check, o => {
            o.Id = "CHECK_FOR_UPDATES";
            o.EnabledWhen = () => UpdateService.Status is not (UpdateStatus.Checking or UpdateStatus.Installing);
        });
        ShellRows.Info(card, "UPDATE_STATUS", () => (UpdateService.Status, UpdateService.Failure, UpdateService.Message, UpdateService.SkippedTag, RestartError()),
            _ => StatusText(), () => UpdateService.Status != UpdateStatus.Idle, () => UpdateService.Status switch {
                UpdateStatus.Failed => Role.Danger,
                UpdateStatus.Available or UpdateStatus.Installed => Role.Accent,
                _ => Role.TextMuted,
            });
        ShellRows.Info(card, "UPDATE_PROGRESS", () => Mathf.RoundToInt(Mathf.Clamp01(UpdateService.Progress) * 100f), n => $"{n}%",
            () => UpdateService.Status == UpdateStatus.Installing && UpdateService.Progress >= 0f);
        ShellRows.Info(card, "UPDATE_VERSION", () => UpdateService.Available, VersionText, () => UpdateReady);
        UpdateButton(card, "UPDATE_NOTES", () => {
            string url = UpdateService.Available?.Url;
            if(!string.IsNullOrEmpty(url)) Application.OpenURL(url);
        }, () => UpdateReady, "DESC_UPDATE_NOTES");
        UpdateButton(card, "UPDATE_SKIP", () => UpdateService.Skip(UpdateService.Available), () => UpdateReady, "DESC_UPDATE_SKIP");
        UpdateButton(card, "UPDATE_INSTALL", () => UpdateService.Install(UpdateService.Available), () => UpdateReady);
        UpdateButton(card, "UPDATE_UNDO", UpdateService.UndoSkip, () => UpdateService.Status == UpdateStatus.Skipped);
        Button restart = null;
        restart = UpdateButton(card, "UPDATE_RESTART", () => Popup.Confirm(restart, "UPDATE_RESTART", "UPDATE_RESTART_CONFIRM", yes => {
            if(!yes) return;
            restartError = null;
            if(GameRestarter.Restart()) return;
            restartError = QuartzHost.Translate("UPDATE_RESTART_FAILED");
            QuartzShell.Surface?.Refresh();
        }), () => UpdateService.Status == UpdateStatus.Installed && GameRestarter.Available, "DESC_UPDATE_RESTART");
        // Only while shown: the search index builds this page detached, and must not subscribe. With no shell there is no card,
        // and nothing to refresh.
        Action changed = () => QuartzShell.Surface?.Refresh();
        if(card != null) Ui.WhileAttached(card, () => UpdateService.OnChanged += changed, () => UpdateService.OnChanged -= changed);
    }

    private static Button UpdateButton(VisualElement card, string key, Action click, Func<bool> visible, string tooltip = null) =>
        Controls.Action(card, "", key, click, o => {
            o.Id = key;
            o.Tooltip = tooltip;
            o.VisibleWhen = visible;
        });

    private static string RestartError() => UpdateService.Status == UpdateStatus.Installed ? restartError : restartError = null;

    // The classic section's status text, by state.
    private static string StatusText() => UpdateService.Status switch {
        UpdateStatus.Checking => QuartzHost.Translate("UPDATE_CHECKING"),
        UpdateStatus.UpToDate => QuartzHost.Translate("UPDATE_UP_TO_DATE"),
        UpdateStatus.Available => QuartzHost.Translate("UPDATE_AVAILABLE"),
        UpdateStatus.Installing => QuartzHost.Translate("UPDATE_DOWNLOADING"),
        UpdateStatus.Installed => RestartError()
            ?? (string.IsNullOrEmpty(UpdateService.Message) ? QuartzHost.Translate("UPDATE_INSTALLED") : UpdateService.Message),
        UpdateStatus.Skipped => string.Format(QuartzHost.Translate("UPDATE_SKIPPED"), UpdateService.SkippedTag),
        UpdateStatus.Failed => QuartzHost.Translate(UpdateService.Failure switch {
            UpdateFailure.Network => "UPDATE_FAILED_NETWORK",
            UpdateFailure.NotFound => "UPDATE_FAILED_NOT_FOUND",
            UpdateFailure.RateLimited => "UPDATE_FAILED_RATE_LIMIT",
            UpdateFailure.InstallError => "UPDATE_FAILED_INSTALL",
            _ => "UPDATE_FAILED_CHECK",
        }),
        _ => "",
    };

    private static string VersionText(UpdateInfo info) {
        if(info == null) return "";
        string name = string.IsNullOrEmpty(info.Name) ? "" : $"  —  {info.Name}";
        string simulated = UpdateService.DevSimulate ? " " + QuartzHost.Translate("UPDATE_SIMULATED") : "";
        return $"v{Info.DisplayVersion}  →  {info.Tag}{name}{simulated}";
    }
}
#endif

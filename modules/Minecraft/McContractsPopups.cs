#nullable enable
namespace VoltstroStudios.UnityWebBrowser.Shared.Popups;
// Registered purely so VoltRpc's connect-time sync sees the same services the
// engine hosts — it hosts IEngineControls and this one, and any disagreement
// aborts the connection with SyncServiceMissMatchException. The namespace must be
// upstream's real one (Shared.Popups, not Shared.Core): VoltRpc keys services by
// full type name. Quartz pins popups to -popup-action Ignore, so nothing calls these.
public interface IPopupClientControls {
    void PopupClose(Guid guid);
    void PopupExecuteJs(Guid guid, string js);
}

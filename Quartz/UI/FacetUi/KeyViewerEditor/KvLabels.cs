#if !IL2CPP && !QUARTZ_KEYVIEWER
using KeyLayoutText = PrismLib.UI.Toolkit.KeyLayoutText;
namespace Quartz.UI.FacetUi.KeyViewerEditor;
/// <summary>
/// The key viewer editor's label ids, each mapped to the Lang key Quartz shows for it: the classic editor's key where it has
/// one, so its translations carry over, else a MENU_KVE_ key. The ids are constants, so this loads no PrismLib.UI type.
/// </summary>
internal static class KvLabels {
    private static readonly Dictionary<string, string> map = new(StringComparer.Ordinal) {
        [KeyLayoutText.Undo] = "KVI_UNDO", [KeyLayoutText.Redo] = "KVI_REDO",
        [KeyLayoutText.Duplicate] = "KVI_DUPLICATE", [KeyLayoutText.Delete] = "KVI_DELETE",
        [KeyLayoutText.CopyStyle] = "KVI_COPY_STYLE", [KeyLayoutText.PasteStyle] = "KVI_PASTE_STYLE",
        [KeyLayoutText.AlignLeft] = "KVI_SNAP_LEFT", [KeyLayoutText.AlignRight] = "KVI_SNAP_RIGHT",
        [KeyLayoutText.AlignTop] = "KVI_SNAP_TOP", [KeyLayoutText.AlignBottom] = "KVI_SNAP_BOTTOM",
        [KeyLayoutText.SpreadH] = "KVI_SPREAD_H", [KeyLayoutText.SpreadV] = "KVI_SPREAD_V",
        [KeyLayoutText.Rebind] = "KVI_REBIND", [KeyLayoutText.Arrange] = "KVI_SEC_ARRANGE",
        [KeyLayoutText.Fit] = "KVI_ZOOM_FIT", [KeyLayoutText.ZoomIn] = "KVI_ZOOM_IN", [KeyLayoutText.ZoomOut] = "KVI_ZOOM_OUT",
        [KeyLayoutText.ZoomReset] = "KVI_ZOOM_RESET", [KeyLayoutText.Snap] = "KVI_SNAP",
        [KeyLayoutText.CanvasHint] = "KVI_TOOLBAR_HINT",
        [KeyLayoutText.Hands] = "KEYVIEWER_EDITOR_HAND_TABS", [KeyLayoutText.Feet] = "KEYVIEWER_EDITOR_FOOT_TABS",
        [KeyLayoutText.HideFeet] = "KEYVIEWER_EDITOR_FOOT_HIDE", [KeyLayoutText.DeleteConfirm] = "KEYVIEWER_EDITOR_TAB_DELETE_CONFIRM",
        [KeyLayoutText.ExportDm] = "KEYVIEWER_EDITOR_EXPORT_JSON",
        [KeyLayoutText.File] = "MENU_KVE_FILE", [KeyLayoutText.Edit] = "MENU_KVE_EDIT", [KeyLayoutText.Insert] = "MENU_KVE_INSERT",
        [KeyLayoutText.View] = "MENU_KVE_VIEW", [KeyLayoutText.Layouts] = "MENU_KVE_LAYOUTS", [KeyLayoutText.NewLayout] = "MENU_KVE_NEW_TAB",
        [KeyLayoutText.Presets] = "MENU_KVE_PRESETS", [KeyLayoutText.Cut] = "MENU_KVE_CUT", [KeyLayoutText.Copy] = "MENU_KVE_COPY",
        [KeyLayoutText.Paste] = "MENU_KVE_PASTE", [KeyLayoutText.SelectAll] = "MENU_KVE_SELECT_ALL",
        [KeyLayoutText.RebindInOrder] = "MENU_KVE_REBIND_ORDER", [KeyLayoutText.ToFront] = "MENU_KVE_TO_FRONT",
        [KeyLayoutText.ToBack] = "MENU_KVE_TO_BACK", [KeyLayoutText.Hide] = "MENU_KVE_HIDE", [KeyLayoutText.Show] = "MENU_KVE_SHOW",
        [KeyLayoutText.Grid] = "MENU_KVE_GRID", [KeyLayoutText.Guides] = "MENU_KVE_GUIDES", [KeyLayoutText.Rename] = "MENU_KVE_RENAME",
        [KeyLayoutText.Cancel] = "MENU_KVE_CANCEL", [KeyLayoutText.Switcher] = "MENU_KVE_SWITCHER", [KeyLayoutText.Hidden] = "MENU_KVE_HIDDEN",
        [KeyLayoutText.NewFromPreset] = "MENU_KVE_NEW_FROM_PRESET", [KeyLayoutText.EditLayout] = "MENU_KVE_EDIT_LAYOUT",
        [KeyLayoutText.MoveLayout] = "MENU_KVE_MOVE_LAYOUT",
    };
    internal static IReadOnlyDictionary<string, string> Map => map;
    /// <summary>The Lang key for a label id; null when unmapped.</summary>
    internal static string KeyFor(string id) => id != null && map.TryGetValue(id, out string key) ? key : null;
}
#endif

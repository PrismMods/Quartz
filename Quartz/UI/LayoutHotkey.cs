using System.Runtime.CompilerServices;
using Quartz.Core;
using Quartz.Prism;
using Quartz.UI.FacetUi;
namespace Quartz.UI;
/// <summary>
/// The menu hotkey with the new menu on: it opens Quartz's shortcut menu, and the shared layout editor that menu's Edit
/// layout opened brings the shortcut menu back once its session ends. With the new menu off, or without the layout filter,
/// the hotkey opens the classic hub as before. ShortcutTrip decides; this is the glue to the game.
/// </summary>
internal static class LayoutHotkey {
    internal static readonly PrismRules.ShortcutTrip Trip = new();
#if DEBUG
    /// <summary>The hub self-tests: the hotkey keeps its old meaning while they run.</summary>
    internal static bool ForceHub;
#endif
    /// <summary>The session on screen is the one the shortcut menu's Edit layout opened.</summary>
    internal static bool Ours => Trip.Ours;

    /// <summary>The hotkey opens the shortcut menu rather than the hub.</summary>
    internal static bool Door {
        get {
#if DEBUG
            if(ForceHub) return false;
#endif
            return UICore.NewMenuWanted && PrismBridge.FilterReady;
        }
    }

    /// <summary>What a press does now. With the door shut and no trip under way it is the old toggle, with no probe.</summary>
    internal static PrismRules.HotkeyRoute Route() {
        bool door = Door, shortcut = FacetBridge.ShortcutVisible();
        if(!door && !Trip.Busy && !shortcut) return PrismRules.HotkeyRoute.Toggle;
        return Trip.Route(door, shortcut, UICore.SharedLayoutOpen, EditorShown(), UICore.MenuShowing);
    }

    internal static void Press(PrismRules.HotkeyRoute r) {
        switch(r) {
            case PrismRules.HotkeyRoute.Toggle:
                UICore.Toggle();
                break;
            case PrismRules.HotkeyRoute.Shortcut:
                FacetBridge.OpenShortcut();
                break;
            case PrismRules.HotkeyRoute.HideShortcut:
                Trip.Cancel();
                FacetBridge.HideShortcut();
                break;
            case PrismRules.HotkeyRoute.CloseEditor:
                Trip.Cancel();
                PrismBridge.CloseLayoutEditor();
                break;
        }
    }

    /// <summary>The shortcut menu's Edit layout: the editor, and the way back to the shortcut menu once it closes.</summary>
    internal static void EditLayout() {
        Trip.Request();
        PrismBridge.OpenLayoutEditor();
    }

    /// <summary>Every frame; does nothing unless a trip is under way.</summary>
    internal static void Tick() {
        if(!Trip.Busy) return;
        PrismRules.TripStep step = Trip.Step(UICore.IsArranging || EditorShown());
        if(step == PrismRules.TripStep.None) return;
        if(step == PrismRules.TripStep.BackLate)
            MainCore.Log.Msg($"[Layout] the layout editor did not appear within {PrismRules.ShortcutTrip.WaitFrames} frames of Edit layout, so the shortcut menu comes back");
        if(!UICore.MenuShowing && !FacetBridge.ShortcutVisible() && Door) FacetBridge.OpenShortcut();
    }

    /// <summary>The shared layout editor is on screen, whichever mod draws it.</summary>
    internal static bool EditorShown() => PrismBridge.UiAvailable && EditorShownImpl();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EditorShownImpl() {
#if IL2CPP
        return false;
#else
        return LayoutEditorProbe.Shown();
#endif
    }
}

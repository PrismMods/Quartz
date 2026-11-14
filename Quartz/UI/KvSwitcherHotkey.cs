using Quartz.Core;
using Quartz.Game.Stats;
using Quartz.IO;
using Quartz.Prism;
using Quartz.UI.FacetUi;
using UnityEngine;
namespace Quartz.UI;
/// <summary>
/// The key viewer layout switcher's hotkey: unbound until the player sets one, and ignored while a level plays (paused, in a
/// menu or in the editor it works). The switcher also hides when play starts, so no window sits over the level.
/// </summary>
internal static class KvSwitcherHotkey {
    private static readonly Func<KeyCode, bool> keyDown = Keybind.KeyDown;
    private static readonly Func<Keybind.KeyModifier, bool> modifierHeld = Keybind.ModifierHeld;
    private static bool wasPlaying;

    /// <summary>The last TrySet was refused; the hotkey row says why.</summary>
    internal static bool Refused { get; private set; }

    internal static void Tick() => Tick(keyDown, modifierHeld, GameStats.InGame);

    internal static void Tick(Func<KeyCode, bool> down, Func<Keybind.KeyModifier, bool> modHeld, bool playing) {
        if(playing && !wasPlaying) FacetBridge.HideKvSwitcher();
        wasPlaying = playing;
        CoreSettings c = MainCore.Conf;
        if(c == null || c.KvSwitcherKey == 0) return;
        if(Allowed(modHeld((Keybind.KeyModifier)c.KvSwitcherModifier), down((KeyCode)c.KvSwitcherKey), playing)) FacetBridge.ToggleKvSwitcher();
    }

    internal static bool Allowed(bool modHeld, bool down, bool playing) => !playing && UICore.ToggleHotkeyAllowed(modHeld, down);

    /// <summary>Saves the chord; key 0 clears it. Refuses a key Quartz won't take as a hotkey, Escape, and a chord the menu hotkey
    /// or a Prism window's chord would also fire on.</summary>
    internal static bool TrySet(int modifier, int key) {
        CoreSettings c = MainCore.Conf;
        bool ok = modifier >= 0 && modifier <= (int)Keybind.KeyModifier.Cmd && Enum.IsDefined(typeof(KeyCode), key)
            && (key == 0 || !PrismRules.SwitcherHotkeyRefused(modifier, key, c.ToggleModifier, c.ToggleKey));
        Refused = !ok;
        if(!ok) return false;
        c.KvSwitcherModifier = modifier;
        c.KvSwitcherKey = key;
        MainCore.ConfMgr.RequestSave();
        return true;
    }
}

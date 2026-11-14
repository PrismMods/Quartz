#if !IL2CPP
using Facet;
using System.Runtime.CompilerServices;
using Quartz.Core;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>The rows the form pages share: hotkeys on any row, sliders that save on release, keys, chords and file picks.</summary>
internal static partial class ShellRows {
    /// <summary>A right-click on the row sets or clears classicId's hotkey, shared with the classic menu, and the tooltip shows
    /// the bound key under o.Tooltip's description. Nothing where classicId is null. row is the built row, which the capture
    /// prompt sits under.</summary>
    internal static void Hotkeys(ControlOptions o, string classicId, Func<VisualElement> row) {
        if(classicId == null) return;
        var menu = new List<KeyValuePair<string, Action>>();
        string tip = o.Tooltip;
        // Clear only while bound.
        void Offer() {
            menu.Clear();
            menu.Add(new("MENU_SET_HOTKEY", () => Capture(row(), classicId, Offer)));
            if(ToggleBinds.TryGet(classicId, out _, out _)) menu.Add(new("MENU_CLEAR_HOTKEY", () => {
                ClearHotkey(classicId);
                Offer();
            }));
        }
        // Asked as the pointer comes over the row, so before any right-click on it: the menu is offered afresh there, as
        // the classic menu may have bound or cleared the key since.
        o.TooltipText = () => {
            Offer();
            return Tip(tip, classicId);
        };
        o.ContextMenu = menu;
        Offer();
    }
    /// <summary>The hotkey menu on a feature page's header (Id headerId), for classicId: the classic Features page's switch.</summary>
    internal static void HeaderHotkey(VisualElement page, string headerId, string classicId) {
        VisualElement head = page.Q(name: headerId);
        if(head != null) Controls.Hook(head, o => Hotkeys(o, classicId, () => head));
    }

    /// <summary>Slider, with live run on every write and released once per committed drag or typed value, and per undo or
    /// redo: the classic row applies and saves on release.</summary>
    internal static Control<float> ReleaseSlider(VisualElement card, string key, Bind<float> bind, float min, float max, float step,
        string format, Action live, Action released, Action<SliderOptions> configure = null) {
        Control<float> c = Slider(card, key, bind, min, max, step, format, live, null, configure);
        Controls.AfterChange(c.Root, released);
        return c;
    }

    /// <summary>A button-only row, Id key, that starts the feature's own capture: start(done) begins it and calls done when it
    /// ends. The button reads PRESS_A_KEY while capturing() holds, and a click then cancels.</summary>
    internal static Button CaptureButton(VisualElement card, string key, Func<bool> capturing, Action<Action> start, Action cancel,
        Action<ControlOptions> configure = null) {
        Button b = null;
        string face = key;
        b = Controls.Action(card, "", key, () => {
            if(capturing()) cancel();
            else start(() => QuartzShell.Surface?.Refresh());
        }, o => {
            o.Id = key;
            configure?.Invoke(o);
            // The refresher asks VisibleWhen on every pass, so the face follows the capture there.
            Func<bool> visible = o.VisibleWhen;
            o.VisibleWhen = () => {
                string want = capturing() ? "PRESS_A_KEY" : key;
                if(want != face && b != null) Words.Label(b, face = want);
                return visible?.Invoke() ?? true;
            };
        });
        EndOnLeave(b, () => {
            if(capturing()) cancel();
        });
        return b;
    }

    /// <summary>One key, shown by name (Keybind.KeyName by default). A capture listens through KeyCaptureService and skips the
    /// keys accept refuses (None and Escape by default); Escape cancels it. A name written another way maps back to its key;
    /// an unknown name writes nothing.</summary>
    internal static Control<string> Key(VisualElement card, string key, Func<KeyCode> get, Action<KeyCode> set, Action save,
        Func<KeyCode, string> name = null, Func<KeyCode, bool> accept = null, Action<KeyOptions> configure = null) {
        name ??= Keybind.KeyName;
        accept ??= k => k is not (KeyCode.None or KeyCode.Escape);
        Dictionary<string, KeyCode> codes = null;
        bool capturing = false;
        void Listen(Action<string> done) {
            bool again = false;
            capturing = true;
            KeyCaptureService.Start(k => {
                if(k == KeyCode.None) done(Facet.Input.ChordCancel);
                else if(accept(k)) done(name(k));
                else again = true;
            }, () => {
                capturing = false;
                if(again) Listen(done);
            });
        }
        Control<string> c = Controls.Key(card, key, Writes(Bind.To(() => name(get()), v => {
            codes ??= Codes(name);
            if(v != null && codes.TryGetValue(v, out KeyCode k)) set(k);
        }), null, null, save), o => {
            o.Id = key;
            o.Prompt = true;
            o.Capture = Listen;
            o.CancelCapture = KeyCaptureService.Cancel;
            configure?.Invoke(o);
        });
        EndOnLeave(c.Root, () => {
            if(capturing) KeyCaptureService.Cancel();
        });
        return c;
    }

    /// <summary>A modifier and a key, shown as Keybind.Format gives them ("Ctrl + F8"), written in one write so an undo puts
    /// both back. A capture waits past a modifier for its key, as the classic hotkey capture does.</summary>
    internal static Control<string> Chord(VisualElement card, string key, Func<(Keybind.KeyModifier mod, KeyCode key)> get,
        Action<Keybind.KeyModifier, KeyCode> set, Action save, Action<KeyOptions> configure = null) {
        Dictionary<string, KeyCode> codes = null;
        bool capturing = false;
        void Listen(Action<string> done) {
            bool again = false;
            capturing = true;
            KeyCaptureService.Start(k => {
                if(Keybind.IsModifier(k)) again = true;
                else if(k == KeyCode.None) done(Facet.Input.ChordCancel);
                else done(Keybind.Format(Keybind.HeldModifier(), k));
            }, () => {
                capturing = false;
                if(again) Listen(done);
            });
        }
        // Facet's own Chord option speaks "Ctrl+F8", not Quartz's "Ctrl + F8", so the row is a plain key row.
        Control<string> c = Controls.Key(card, key, Writes(Bind.To(() => {
            (Keybind.KeyModifier mod, KeyCode k) = get();
            return Keybind.Format(mod, k);
        }, v => {
            if(!PageRules.SplitChord(v, out string left, out string right)) return;
            codes ??= Codes(Keybind.KeyName);
            Keybind.KeyModifier mod = Keybind.KeyModifier.None;
            if(left.Length > 0) {
                Keybind.KeyModifier[] named = [.. Enum.GetValues(typeof(Keybind.KeyModifier)).Cast<Keybind.KeyModifier>()
                    .Where(m => m != Keybind.KeyModifier.None && Keybind.ModifierName(m) == left)];
                if(named.Length > 0) mod = named[0];
                else right = v;   // not a modifier: the whole string names the key
            }
            if(codes.TryGetValue(right, out KeyCode code)) set(mod, code);
        }), null, null, save), o => {
            o.Id = key;
            o.Prompt = true;
            o.Capture = Listen;
            o.CancelCapture = KeyCaptureService.Cancel;
            configure?.Invoke(o);
        });
        EndOnLeave(c.Root, () => {
            if(capturing) KeyCaptureService.Cancel();
        });
        return c;
    }

    // Each name's key, the first code of a name winning: KeyCode has aliases of one value (LeftCommand, LeftApple).
    private static Dictionary<string, KeyCode> Codes(Func<KeyCode, string> name) {
        var codes = new Dictionary<string, KeyCode>();
        foreach(KeyCode k in Enum.GetValues(typeof(KeyCode))) {
            string n = name(k);
            if(n != null && !codes.ContainsKey(n)) codes[n] = k;
        }
        return codes;
    }

    // Hiding the menu only sets display, so a capture is ended on the surface's Hidden too, not only on leaving the panel.
    private static void EndOnLeave(VisualElement row, Action end) {
        Surface surface = null;
        Ui.WhileAttached(row, () => {
            if(surface != null) surface.Hidden -= end;
            surface = QuartzShell.Surface;
            if(surface != null) surface.Hidden += end;
        }, () => {
            if(surface != null) surface.Hidden -= end;
            surface = null;
            end();
        });
    }

    /// <summary>A file's status line, Id pickKey + "_STATUS", then the Pick and Clear buttons as rows of their own; descKey is
    /// the Pick row's tooltip.</summary>
    internal static void FilePick(VisualElement card, string pickKey, string clearKey, Func<string> status, Action pick, Action clear,
        string descKey = null) {
        Info(card, pickKey + "_STATUS", status, s => s);
        Controls.Action(card, "", pickKey, pick, o => {
            o.Id = pickKey;
            o.Tooltip = descKey;
        });
        Controls.Action(card, "", clearKey, clear, o => o.Id = clearKey);
    }

    private static readonly ConditionalWeakTable<object, string> Serials = new();
    private static int serial;

    /// <summary>A list item's key for Controls.Repeat: the object's serial and its index, so its box rebuilds when it moves (its
    /// title and #n Ids renumber), not when a later item goes.</summary>
    internal static string ItemKey(object item, int index) => Serials.GetValue(item, _ => (++serial).ToString()) + ":" + index;

    /// <summary>An UndoDocument for a row in a list item: the item while list() holds it, else null, so its steps go stale
    /// with the item.</summary>
    internal static Func<object> ItemDocument(object item, Func<System.Collections.IList> list) => () => {
        System.Collections.IList now = list();
        if(now != null)
            foreach(object o in now)
                if(ReferenceEquals(o, item)) return item;
        return null;
    };
}
#endif

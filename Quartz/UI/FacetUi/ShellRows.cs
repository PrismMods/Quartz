#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Overlay;
using Quartz.Resource;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The rows the Overlay pages share, each the classic row's twin: the same field, then the overlay's apply and save
/// (save is a debounced request, so a save per change is fine). A row's Id is its label key.
/// </summary>
internal static class ShellRows {
    // Written through QuartzShell.Writes, so the classic menu rebuilds before it next shows.
    private static Bind<T> Writes<T>(Bind<T> bind, Action<T> after, Action apply, Action save) => QuartzShell.Writes(bind.Get, v => {
        bind.Set(v);
        after?.Invoke(v);
        apply?.Invoke();
        save?.Invoke();
    });

    /// <summary>A switch. classicId is the classic toggle's id, whose hotkey this row shares: a right-click sets or clears it.
    /// Null where the classic toggle has none.</summary>
    internal static Control<bool> Toggle(VisualElement card, string key, string classicId, Bind<bool> bind, Action apply, Action save,
        Action<Options<bool>> configure = null) {
        // The classic toggles show the value too: a hotkey fires one of them, and flips from what it shows.
        var menu = new List<KeyValuePair<string, Action>>();
        Control<bool> c = null;
        c = Controls.Toggle(card, key, Writes(bind, v => ToggleBinds.Show(classicId, v), apply, save), o => {
            o.Id = key;
            configure?.Invoke(o);
            if(classicId == null) return;
            string tip = o.Tooltip;
            // Asked as the pointer comes over the row, so before any right-click on it: the menu is offered afresh there, as
            // the classic menu may have bound or cleared the key since.
            o.TooltipText = () => {
                Offer();
                return Tip(tip, classicId);
            };
            o.ContextMenu = menu;
        });
        if(classicId == null) return c;
        // Clear only while bound.
        void Offer() {
            menu.Clear();
            menu.Add(new("MENU_SET_HOTKEY", () => Capture(c!.Root, classicId, Offer)));   // picked from the built row
            if(ToggleBinds.TryGet(classicId, out _, out _)) menu.Add(new("MENU_CLEAR_HOTKEY", () => {
                ClearHotkey(classicId);
                Offer();
            }));
        }
        Offer();
        return c;
    }

    // The description, and the bound key on a line of its own.
    private static string Tip(string descKey, string classicId) {
        string desc = descKey == null ? "" : QuartzHost.Translate(descKey);
        if(!ToggleBinds.TryGet(classicId, out Keybind.KeyModifier mod, out KeyCode key)) return desc;
        string bound = Keybind.Format(mod, key);
        return desc.Length == 0 ? bound : desc + "\n" + bound;
    }

    /// <summary>Binds a toggle's hotkey, shared with the classic menu, whose row shows it once it rebuilds.</summary>
    internal static void SetHotkey(string classicId, Keybind.KeyModifier mod, KeyCode key) {
        ToggleBinds.Set(classicId, mod, key);
        UICore.ClassicStale = true;
    }

    internal static void ClearHotkey(string classicId) {
        ToggleBinds.Clear(classicId);
        UICore.ClassicStale = true;
    }

    // The next key binds, with the modifier held as it goes down. A prompt shows under the row until the capture ends;
    // Escape or a click outside cancels it. Backspace or Delete clears the bind, as in the classic menu.
    private static void Capture(VisualElement row, string classicId, Action done) {
        bool waiting = true, again = false;
        Popup prompt = Popup.Open(row, content => Ui.Muted("PRESS_A_KEY", content).name = "menu-hotkey-press", () => {
            if(waiting) KeyCaptureService.Cancel();
        });
        void Listen() => KeyCaptureService.Start(k => {
            // A chord's modifier goes down first: keep listening for its key.
            if(Keybind.IsModifier(k)) again = true;
            else if(k is KeyCode.Backspace or KeyCode.Delete) ClearHotkey(classicId);
            else if(k != KeyCode.None) SetHotkey(classicId, Keybind.HeldModifier(), k);
        }, () => {
            if(again) {
                again = false;
                Listen();
                return;
            }
            waiting = false;
            prompt?.Close();
            done();
        });
        Listen();
    }

    internal static Control<float> Slider(VisualElement card, string key, Bind<float> bind, float min, float max, float step, string format,
        Action apply, Action save, Action<SliderOptions> configure = null) =>
        Controls.Slider(card, key, Writes(bind, null, apply, save), o => {
            o.Id = key;
            o.Min = min;
            o.Max = max;
            o.Step = step;
            o.Format = format;
            configure?.Invoke(o);
        });

    /// <summary>A text field that holds limit characters, as the classic field does: typing stops there, and text written past
    /// it another way (a paste, an undo) is cut as it is written.</summary>
    internal static Control<string> Text(VisualElement card, string key, Bind<string> bind, int limit, Action apply, Action save,
        Action<TextOptions> configure = null) {
        Control<string> c = Controls.Text(card, key, Writes(Bind.To(bind.Get, v => bind.Set(v != null && v.Length > limit ? v[..limit] : v)), null, apply, save),
            o => {
                o.Id = key;
                configure?.Invoke(o);
            });
        if(c.Input is TextField field) field.maxLength = limit;
        return c;
    }

    /// <summary>A read-only status line, Id id. Every refresh reads it, so its text is made again only when input or the
    /// language changes, or the language files load again. Nothing is kept while they load: the table is half there.</summary>
    internal static Control<string> Info<T>(VisualElement card, string id, Func<T> input, Func<T, string> text, Func<bool> visible = null) {
        T last = default;
        string language = null, shown = null;
        int loads = 0;
        return Controls.Info(card, "", () => {
            T now = input();
            if(MainCore.Tr.IsLoading) return text(now);
            if(shown == null || loads != QuartzHost.Loads || language != MainCore.Tr.Language || !EqualityComparer<T>.Default.Equals(now, last))
                (last, language, loads, shown) = (now, MainCore.Tr.Language, QuartzHost.Loads, text(now));
            return shown;
        }, o => {
            o.Id = id;
            o.VisibleWhen = visible;
        });
    }

    internal static Control<Color> Colour(VisualElement card, string key, Func<Color> get, Action<Color> set, Action apply, Action save,
        bool alpha = true, Action<ColourOptions> configure = null) =>
        Controls.Colour(card, key, Writes(Bind.To(get, set), null, apply, save), o => {
            o.Id = key;
            o.Alpha = alpha;
            configure?.Invoke(o);
        });

    // In OverlayAnchor's order, after Closest.
    private static readonly string[] Anchors = ["ANCHOR_CLOSEST", "ANCHOR_TOP_LEFT", "ANCHOR_TOP_CENTER", "ANCHOR_TOP_RIGHT", "ANCHOR_MIDDLE_LEFT",
        "ANCHOR_MIDDLE_CENTER", "ANCHOR_MIDDLE_RIGHT", "ANCHOR_BOTTOM_LEFT", "ANCHOR_BOTTOM_CENTER", "ANCHOR_BOTTOM_RIGHT"];

    /// <summary>Closest, then the nine anchors. Closest snaps every element to its nearest anchor; an anchor is the overlay's
    /// SetAnchor, which moves the element to that anchor's default spot, as the classic row does.</summary>
    internal static Control<int> Anchor(VisualElement card, string labelKey, Func<int> anchor, Func<bool> closest, Action<int> setAnchor,
        Action<bool> setClosest) =>
        Controls.Dropdown(card, labelKey, QuartzShell.Writes(() => closest() ? 0 : anchor() + 1, v => {
            setClosest(v == 0);
            if(v == 0) Reorganizer.SnapAllAnchors();
            else setAnchor(v - 1);
        }), o => {
            o.Id = labelKey;
            o.Items = Anchors;
            o.Default = 0;
        });

    /// <summary>The overlay's font, or the global one. The page rebuilds when a font is added or the global one changes
    /// (QuartzShell follows FontManager), so the list is read once here. Items are plain text: Facet wants a Unity Font for
    /// an item's face, and these are TMP assets.</summary>
    internal static Control<int> Font(VisualElement card, string overlayKey) {
        List<string> values = [.. OverlayFontPicker.Values()];
        return Controls.Dropdown(card, "FONT_OVERLAY", QuartzShell.Writes(() => values.IndexOf(OverlayFontPicker.Current(overlayKey)),
            v => FontManager.SetOverlayFont(overlayKey, values[v])), o => {
            o.Id = "FONT_OVERLAY";
            o.Items = [.. values.Select(OverlayFontPicker.Display)];
            o.Default = 0;
        });
    }

    internal static Button ResetPosition(VisualElement card, string key, Action reset) => Controls.Action(card, "", key, reset, o => o.Id = key);

    /// <summary>A shadow's switch, then its offset, softness and colour folded under Advanced, shown while it is on. Every
    /// shadow switch's classic id is its key in lower case. def holds the classic rows' defaults, which a middle-click restores.</summary>
    internal static void Shadow(VisualElement card, string toggleKey, string xKey, string yKey, string softKey, string colourKey,
        Bind<bool> on, Bind<float> x, Bind<float> y, Bind<float> soft, Func<Color> get, Action<Color> set,
        (bool On, float X, float Y, float Soft, Color Colour) def, float xyRange, Action apply, Action save) {
        Toggle(card, toggleKey, toggleKey.ToLowerInvariant(), on, apply, save, o => o.Default = def.On);
        VisualElement more = Controls.Advanced(card);
        Func<bool> shown = on.Get;
        Slider(more, xKey, x, -xyRange, xyRange, 0.1f, "0.0 px", apply, save, o => { o.Default = def.X; o.VisibleWhen = shown; });
        Slider(more, yKey, y, -xyRange, xyRange, 0.1f, "0.0 px", apply, save, o => { o.Default = def.Y; o.VisibleWhen = shown; });
        Slider(more, softKey, soft, 0f, 20f, 0.1f, "0.0 px", apply, save, o => { o.Default = def.Soft; o.VisibleWhen = shown; });
        Colour(more, colourKey, get, set, apply, save, configure: o => { o.Default = def.Colour; o.VisibleWhen = shown; });
    }
}
#endif

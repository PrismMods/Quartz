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
internal static partial class ShellRows {
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
        Control<bool> c = null;
        c = Controls.Toggle(card, key, Writes(bind, ShowOnClassic(classicId), apply, save), o => {
            o.Id = key;
            configure?.Invoke(o);
            Hotkeys(o, classicId, () => c!.Root);   // picked from the built row
        });
        return c;
    }

    // Built apart from Toggle so the bind, which History keeps, shares no closure with the row.
    private static Action<bool> ShowOnClassic(string classicId) => v => ToggleBinds.Show(classicId, v);

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
        Popup prompt = Popup.Open(row, content => Ui.Muted(content, "PRESS_A_KEY").name = "menu-hotkey-press", () => {
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

    /// <summary>A text field that holds limit characters, as the classic field does: typing stops there, and a longer paste is
    /// cut when it is committed (Facet's MaxLength). A limit of 0 or less is none, as on the classic inputs that have
    /// none.</summary>
    internal static Control<string> Text(VisualElement card, string key, Bind<string> bind, int limit, Action apply, Action save,
        Action<TextOptions> configure = null) =>
        Controls.Text(card, key, Writes(bind, null, apply, save), o => {
            o.Id = key;
            o.MaxLength = limit;
            configure?.Invoke(o);
        });

    /// <summary>A read-only status line, Id id. Every refresh reads it, so its text is made again only when input or the
    /// language changes, or the language files load again. Nothing is kept while they load: the table is half there. tone,
    /// when given, paints the text in a role (Danger for a problem), asked on every refresh.</summary>
    internal static Control<string> Info<T>(VisualElement card, string id, Func<T> input, Func<T, string> text, Func<bool> visible = null,
        Func<Role> tone = null) {
        T last = default;
        string language = null, shown = null;
        int loads = 0;
        return Controls.Info(card, "", () => {
            T now = input();
            if(MainCore.Tr.IsLoading) return text(now);
            if(shown == null || loads != QuartzHost.Loads || language != MainCore.Tr.Language || !EqualityComparer<T>.Default.Equals(now, last))
                (last, language, loads, shown) = (now, MainCore.Tr.Language, QuartzHost.Loads, text(now));
            return shown;
        }, (InfoOptions o) => {
            o.Id = id;
            o.VisibleWhen = visible;
            o.Tone = tone;
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
        Action<bool> setClosest, Action<DropdownOptions> configure = null) =>
        Controls.Dropdown(card, labelKey, QuartzShell.Writes(() => closest() ? 0 : anchor() + 1, v => {
            setClosest(v == 0);
            if(v == 0) Reorganizer.SnapAllAnchors();
            else setAnchor(v - 1);
        }), o => {
            o.Id = labelKey;
            o.Items = Anchors;
            o.Default = 0;
            configure?.Invoke(o);
        });

    /// <summary>The overlay's font, or the global one. The page rebuilds when a font is added or the global one changes
    /// (QuartzShell follows FontManager), so the list is read once here. Each item shows in its own face, as on the classic
    /// picker; the global item in the global font.</summary>
    internal static Control<string> Font(VisualElement card, string overlayKey) =>
        Controls.Dropdown(card, "FONT_OVERLAY", QuartzShell.Writes(() => OverlayFontPicker.Current(overlayKey),
            v => FontManager.SetOverlayFont(overlayKey, v)), [.. OverlayFontPicker.Values()], OverlayFontPicker.Display, o => {
            o.Id = "FONT_OVERLAY";
            o.Default = FontManager.FollowGlobal;
            o.ItemFace = name => ShellSettings.Face(name == FontManager.FollowGlobal ? FontManager.Current : FontManager.GetFont(name));
        });

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

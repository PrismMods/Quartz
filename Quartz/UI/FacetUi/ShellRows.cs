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

    /// <summary>An unlabeled button for Buttons: key is its text and its row's Id, tip its tooltip key; more sets the rest of
    /// its row's options (another Id, Undoable).</summary>
    internal readonly struct Btn(string key, Action click, string tip = null, Func<bool> visible = null, Func<bool> enabled = null,
        bool danger = false, Action<ControlOptions> more = null) {
        internal string Key { get; } = key;
        internal Action Click { get; } = click;
        internal string Tip { get; } = tip;
        internal Func<bool> Visible { get; } = visible;
        internal Func<bool> Enabled { get; } = enabled;
        internal bool Danger { get; } = danger;
        internal Action<ControlOptions> More { get; } = more;
    }

    /// <summary>A card's unlabeled buttons on one row, in order. Each is built as Facet's own unlabeled Action (or Danger) row,
    /// so it keeps its row Id, tooltip and search entry; the later rows then move into the first row's control, where a
    /// search hit still finds, scrolls to and flashes each. The first row shows while any of its buttons does.</summary>
    // Facet workaround: Facet gives each unlabeled button a whole row of its own.
    internal static Button[] Buttons(VisualElement card, params Btn[] buttons) {
        var made = new Button[buttons.Length];
        Func<bool> any = buttons.Any(x => x.Visible == null) ? null : () => buttons.Any(x => x.Visible());
        for(int i = 0; i < buttons.Length; i++) {
            Btn x = buttons[i];
            bool first = i == 0;
            Action<ControlOptions> opts = o => {
                o.Id = x.Key;
                o.Tooltip = x.Tip;
                o.VisibleWhen = first ? any : x.Visible;
                if(!first) o.EnabledWhen = x.Enabled;
                x.More?.Invoke(o);
            };
            Button b = made[i] = x.Danger ? Controls.Danger(card, "", x.Key, x.Click, opts) : Controls.Action(card, "", x.Key, x.Click, opts);
            if(!first) {
                Inline(made[0], b);
                continue;
            }
            if(x.Enabled != null || x.Visible != null) Controls.Hook(b, o => { o.VisibleWhen = x.Visible; o.EnabledWhen = x.Enabled; });
            // The row's tooltip would answer for the buttons that join it: the first button's goes on the button (search keeps it).
            if(x.Tip == null) continue;
            Words.Tip(b.parent.parent, null);
            Words.Tip(b, x.Tip);
        }
        return made;
    }

    /// <summary>Moves b's unlabeled row (a Controls.Action or Danger) into host's row, after host's buttons.</summary>
    internal static void Inline(Button host, Button b) => Join(host.parent, b);

    /// <summary>Moves b's unlabeled row into bar (a row of controls, or another row's control box), after what it holds; returns
    /// the moved row.</summary>
    internal static VisualElement Join(VisualElement bar, Button b) {
        VisualElement row = b.parent.parent;
        bar.Add(row);
        row.style.paddingLeft = Theme.For(row).Sizes.Gap;
        row.style.paddingRight = 0f;
        row.style.borderTopWidth = 0f;   // a card's later rows draw a rule above them
        row.style.marginBottom = 0f;
        row[0].style.display = DisplayStyle.None;   // the empty label, which would grow
        Unrow(row);
        return row;
    }

    /// <summary>Puts a note (a status or an empty-list text) at the left of first's button row, where a row's label sits, centred
    /// with the buttons; the buttons stay at the right.</summary>
    internal static T Lead<T>(Button first, T note) where T : VisualElement {
        VisualElement row = first?.parent?.parent;
        if(row == null || note == null) return note;
        row.Insert(0, note);
        note.style.paddingLeft = note.style.paddingRight = 0f;
        note.style.flexShrink = 1f;
        note.style.minWidth = 0f;
        note.style.alignSelf = Align.Center;
        return note;
    }

    /// <summary>A drag grip (two columns of three dots) at the left of a reorderable list item, with the move cursor over it. A
    /// press on it reaches the item, so it starts the list's own drag.</summary>
    internal static VisualElement Grip(VisualElement item) {
        var grip = new VisualElement { name = "grip" };
        grip.style.flexDirection = FlexDirection.Row;
        grip.style.alignSelf = Align.Center;
        grip.style.flexShrink = 0f;
        grip.style.marginRight = Theme.For(item).Sizes.Gap;
        for(int c = 0; c < 2; c++) {
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.marginRight = c == 0 ? 2f : 0f;
            for(int r = 0; r < 3; r++) {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.style.width = dot.style.height = 3f;
                dot.style.marginTop = r == 0 ? 0f : 2f;
                Ui.SetRadius(dot, 1.5f);
                Paint.Bg(dot, Role.TextMuted);
                column.Add(dot);
            }
            grip.Add(column);
        }
        item.Insert(0, grip);
        Cursors.Set(grip, Cursors.Kind.Move);
        return grip;
    }

    /// <summary>A moved row stops lighting on hover: inside another row it would light a box around its button over the
    /// host row's own hover. Only the button under the pointer shows hover.</summary>
    internal static void Unrow(VisualElement row) => Paint.Look(row, new Look());

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

    // Facet workaround: a card pads only its rows, so a bare header or note sits at the card's edge; this gives it the rows' inset.
    internal static T Inset<T>(T e) where T : VisualElement {
        Sizes sz = Theme.For(e).Sizes;
        e.style.paddingLeft = sz.Pad;
        e.style.paddingRight = sz.Pad;
        return e;
    }

    /// <summary>Facet's Tabs.Pages in a card: the first row starts right under the tab rule. Facet leaves a TabGap under the tabs,
    /// and the row after it draws a second rule (the card counts every tab's rows, so after a switch it is not the first).</summary>
    internal static Tabs CardTabs(VisualElement card, IList<NavItem> pages, int selected = -1, Action<int> onSelect = null) {
        Tabs t = null;
        t = Tabs.Pages(card, pages, selected, i => {
            Flush(t);
            onSelect?.Invoke(i);
        });
        t.Element.style.marginBottom = 0f;
        Flush(t);
        return t;
    }

    private static void Flush(Tabs t) {
        if(t?.Content is { childCount: > 0 } c) c[0].style.borderTopWidth = 0f;
    }

    /// <summary>Facet's card header, its text starting where the card's row labels do.</summary>
    internal static Label Header(VisualElement card, string key) => Inset(Controls.Header(card, key));

    // Facet workaround: a section's title sits after its fold arrow (18 px), its rows at the card's inset. The body takes the
    // arrow's width so the rows start under the title.
    private const float SectionArrow = 18f;

    /// <summary>Facet's Section, its rows starting under its title.</summary>
    internal static VisualElement Section(VisualElement parent, string key, Action<SectionOptions> configure) =>
        Indent(Controls.Section(parent, key, configure));

    /// <summary>Facet's Section, its rows starting under its title.</summary>
    internal static VisualElement Section(VisualElement parent, string key, bool defaultOpen = true) =>
        Indent(Controls.Section(parent, key, defaultOpen));

    // Facet workaround too: a section's head stands a Pad off the row above it, outside its rule, and only its text's height tall,
    // so in a card the row above looked padded underneath and a shut section's head sat tight. A head is a row like any other.
    private static VisualElement Indent(VisualElement body) {
        body.style.paddingLeft = SectionArrow;
        VisualElement parent = body.parent;
        int at = parent?.IndexOf(body) ?? -1;
        if(at > 0) {
            VisualElement head = parent![at - 1];
            head.style.marginTop = 0f;
            head.style.minHeight = Theme.For(head).Sizes.RowHeight;
            head.style.alignItems = Align.Center;
        }
        return body;
    }

    /// <summary>A page's left column that swaps to an inspector (Key Viewer, Panels): the settings, and in their place while
    /// inspecting holds, a column named name with Back (backKey; back clears what is inspected) above it. The preview on the
    /// right keeps its height. The arriving column slides in; the settings' scroll is kept on the way in and put back on the
    /// way out. Build the settings into Settings and the inspector's rows into Inspector, after Back.</summary>
    internal static (VisualElement Settings, VisualElement Inspector) Swap(VisualElement left, string name, string backKey, Func<bool> inspecting,
        Action back) {
        ScrollView sv = null;
        VisualElement settings = null, inspector = null;
        float kept = 0f;
        bool was = false;
        bool Now() {
            bool now = inspecting();
            if(now == was) return now;
            was = now;
            Anim.SlideIn(now ? inspector : settings, 8f, Anim.Fast);   // the column that arrives; nothing runs while it stays
            sv ??= left.GetFirstAncestorOfType<ScrollView>();
            if(sv == null) return now;
            if(now) {
                kept = sv.scrollOffset.y;
                sv.scrollOffset = Vector2.zero;
            }
            else ScrollBack(sv, kept);
            return now;
        }
        settings = Controls.When(left, () => !Now());
        inspector = Controls.When(left, Now);
        inspector.name = name;
        Inset(inspector);   // the cards' row inset: Back and the inspector's title line up with the settings' labels
        // Back is a text link: "← label" underlined, muted at rest and full text on hover, no box; the focus ring still shows.
        Button b = Ui.Btn(inspector, backKey, back);
        b.name = backKey;
        b.style.alignSelf = Align.FlexStart;
        b.style.marginBottom = Theme.For(b).Sizes.Gap;
        b.style.paddingLeft = b.style.paddingRight = 0f;
        Paint.Look(b, new Look { Text = Role.TextMuted, HoverText = Role.Text, FocusBorder = Role.Focus });
        void Underline() => b.text = "<u>" + QuartzHost.Translate(backKey) + "</u>";
        Underline();
        Words.OnRelabel(b, Underline);
        return (settings, inspector);
    }

    // The settings lay out again over the next frames once shown: until then the offset would be clamped to the inspector's height.
    private static void ScrollBack(ScrollView sv, float y) {
        int frames = 5;
        IDisposable tick = null;
        tick = Ui.Every(sv, 0, () => {
            sv.scrollOffset = new Vector2(sv.scrollOffset.x, y);
            if(--frames <= 0) tick?.Dispose();
        });
    }
}
#endif

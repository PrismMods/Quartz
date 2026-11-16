#if !IL2CPP
using Facet;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Overlay;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.OverlayPages;
/// <summary>
/// One panel's stats: Add Stat (a popup picker, one fold per category) and Clear All Stats on one row, then the stats one
/// line each in their order, dragged by their grip to reorder and clicked (here or on the preview) to inspect. The inspected
/// stat's card takes the settings' place (ShellRows.Swap): its text or image field, Show, Label, Swap (the same picker, whose
/// pick replaces it) and Delete on one tab, its Setting rows on another. Ids of a stat's rows are numbered across the page, as the
/// classic page lists them: the stat's own label (PANEL_STAT_&lt;ID&gt;) counts that stat, the rest count every stat.
/// </summary>
internal static class PanelsStats {
    private static List<PanelConfig> Panels => PanelsOverlay.Conf.Panels;
    private static readonly string[] Categories = ["Accuracy", "Time", "BPM", "Map Stats", "Other"];
    private static readonly Color PerfectDefault = new(1f, 0.854902f, 0f, 1f);

    // The picker's open category, kept for the session: the next picker opens on it.
    private static string openCategory;

    /// <summary>The stat picker in a popup under anchor: one fold per category (the last opened, else the first, open), its stats
    /// as compact buttons; a stat already on the panel shows disabled (text and image never do). A pick adds a stat, or with
    /// replace set swaps that stat's kind, then closes the popup; Escape or a click outside closes it.</summary>
    internal static Popup Picker(VisualElement anchor, PanelConfig panel, int n, StatEntry replace) {
        Popup popup = null;
        void Pick(string id) {
            popup?.Close();
            Dictionary<object, int> before = PanelsList.Places(Panels);
            if(replace != null) {
                if(!panel.Stats.Contains(replace)) return;
                replace.Id = id;
            } else panel.Stats.Add(new StatEntry(id) { ShowLabel = id is not ("text" or "image") });
            Changed(before);
        }
        popup = Popup.Open(anchor, content => {
            content.name = "panel-stat-picker";
            content.style.width = 320f;
            Sizes sz = Theme.For(content).Sizes;
            // The card's standard inset (Facet's popup gives 4 px), every section a Gap apart.
            content.style.paddingLeft = sz.Pad; content.style.paddingRight = sz.Pad;
            content.style.paddingTop = sz.Gap; content.style.paddingBottom = sz.Gap;
            IReadOnlyList<PanelsOverlay.StatDef> all = PanelsOverlay.AllStats;
            List<string> categories = [.. Categories];
            foreach(PanelsOverlay.StatDef stat in all)
                if(!categories.Contains(stat.Category)) categories.Add(stat.Category);
            categories.RemoveAll(c => !all.Any(d => d.Category == c));
            string shown = categories.Contains(openCategory) ? openCategory : categories.FirstOrDefault();
            var bodies = new List<(string Category, Button Head, VisualElement Body)>();
            void Say(Button head, string category, bool open) =>
                // Text set by hand is not translated by Facet: Quartz translates it (QuartzShell.Label is a key, for Ui calls).
                head.text = (open ? "▾ " : "▸ ") + QuartzHost.Text(Quartz.Localization.LocaleKeys.FromText("PANEL_CATEGORY", category), category);
            foreach(string category in categories) {
                string c = category;
                Button head = Ui.Btn(content, "", null);
                head.name = Quartz.Localization.LocaleKeys.FromText("PANEL_CATEGORY", c) + "#" + n;
                Paint.Look(head, Theme.For(head).Looks.Row);
                head.style.unityTextAlign = TextAnchor.MiddleLeft;
                head.style.marginRight = 0f;
                head.style.paddingLeft = 0f;
                var body = new VisualElement { name = "panel-picker-items" };
                body.style.flexDirection = FlexDirection.Row;
                body.style.flexWrap = Wrap.Wrap;
                body.style.paddingLeft = sz.Pad;
                body.style.paddingBottom = sz.Gap;
                content.Add(body);
                foreach(PanelsOverlay.StatDef stat in all) {
                    if(stat.Category != c) continue;
                    string id = stat.Id;
                    Button b = Ui.Btn(body, QuartzShell.Label(StatKey(id), DefaultLabel(id)), () => Pick(id));
                    b.name = Quartz.Localization.LocaleKeys.FromText("PANEL_PICK", id) + "#" + n + "-input";
                    b.style.marginRight = sz.Gap; b.style.marginBottom = sz.Gap;
                    b.SetEnabled(id is "text" or "image" || !panel.Stats.Exists(e => e.Id == id && e != replace));
                }
                bodies.Add((c, head, body));
                head.clicked += () => {
                    openCategory = c;
                    foreach(var (cat, h, bd) in bodies) {
                        bool on = cat == c && bd.style.display == DisplayStyle.None;
                        bd.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                        Say(h, cat, on);
                    }
                };
                bool open = c == shown;
                body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                Say(head, c, open);
            }
        });
        return popup;
    }

    /// <summary>Panel n's stat rows: Add Stat and Clear All Stats, the picker while open, then the stats one line each.</summary>
    internal static void Build(VisualElement box, PanelConfig panel, int n) {
        Button add = null;
        add = ShellRows.Buttons(box, new ShellRows.Btn("PANEL_ADDSTAT", () => Picker(add, panel, n, null), "DESC_PANEL_ADDSTAT",
            more: o => o.Id = "PANEL_ADDSTAT#" + n), new ShellRows.Btn("PANEL_CLEARSTATS", () => {
            Dictionary<object, int> before = PanelsList.Places(Panels);
            panel.Stats.Clear();
            Changed(before);
        }, danger: true, more: o => o.Id = "PANEL_CLEARSTATS#" + n))[0];
        Controls.Info(box, "PANEL_NO_STATS", () => "", (InfoOptions o) => {
            o.Id = "PANEL_NO_STATS#" + n;
            o.VisibleWhen = () => panel.Stats.Count == 0;
        });
        // One line per stat: dragged into a new order, clicked to inspect it in the left column's place.
        Controls.Reorder<StatEntry>(box, o => {
            o.Id = "PANEL_STATS#" + n;
            o.Vertical = true;
            o.Items = () => panel.Stats;
            o.Key = s => ItemKey(panel, s);
            o.Text = s => QuartzHost.Text(StatKey(s.Id), DefaultLabel(s.Id));
            o.Move = (from, to) => Move(panel, from, to);
            o.Selected = s => s == Selected(panel);
            o.Select = s => {
                Select(panel, s);
                QuartzShell.Surface?.Refresh();
            };
            // The stat's own label is its classic row (PANEL_STAT_<ID>), counted per stat.
            o.Build = (e, s) => {
                ShellRows.Grip(e);
                if(s.Id is not ("text" or "image") && e.Q<Label>("PANEL_STATS#" + n + "-text") is Label label) label.name = StatKey(s.Id) + "#" + Count(panel, s);
            };
        });
    }

    /// <summary>The inspector column: the inspected stat's card, built again when another stat is inspected or it moves.</summary>
    internal static void InspectorColumn(VisualElement column) {
        Controls.Live(column, () => column.panel != null && PanelsList.Shown is { } p && Selected(p) is { } s ? ItemKey(p, s) : "", e => {
            if(column.panel == null || PanelsList.Shown is not { } panel || Selected(panel) is not { } s) return;
            int n = Panels.IndexOf(panel) + 1;
            Inspector(e, panel, s, n, ShellRows.ItemDocument(panel, () => Panels), anchor => Picker(anchor, panel, n, s), () => {
                Dictionary<object, int> before = PanelsList.Places(Panels);
                panel.Stats.Remove(s);
                Changed(before);   // the inspected stat is gone: the settings come back
            });
        });
    }

    /// <summary>The stat inspected on panel (the one last clicked in the list or the preview), or null: the settings show.</summary>
    internal static StatEntry Selected(PanelConfig panel) =>
        PanelsOverlay.PreviewStat is { } s && panel != null && panel.Stats.Contains(s) ? s : null;

    /// <summary>Inspects s on panel (picking panel): its rows take the settings' place and the preview marks its line.</summary>
    internal static void Select(PanelConfig panel, StatEntry s) {
        PanelsList.Show(panel);
        PanelsOverlay.PreviewStat = s;
    }

    /// <summary>Whether a stat of the picked panel is inspected: the left column shows its inspector.</summary>
    internal static bool Inspecting() => Selected(PanelsList.Shown) != null;

    /// <summary>Back: the settings again.</summary>
    internal static void Deselect() {
        PanelsOverlay.PreviewStat = null;
        QuartzShell.Surface?.Refresh();
    }

    /// <summary>A press on the preview: the stat whose line is under it is inspected.</summary>
    internal static void ClickPreview(Vector3 world) {
        if(PanelsList.Shown is not { } panel || PanelsOverlay.StatAt(world) is not { } s || !panel.Stats.Contains(s)) return;
        Select(panel, s);
        QuartzShell.Surface?.Refresh();
    }

    /// <summary>A search hit on a stat's row: the picked panel's first stat is inspected, on its Stat tab.</summary>
    internal static void InspectFirst() {
        WantTab = "PANEL_TAB_STAT";
        if(PanelsList.Shown is { Stats.Count: > 0 } p && Selected(p) == null) Select(p, p.Stats[0]);
    }

    /// <summary>The inspector tab the next inspector opens on (a search hit's), then cleared; null: the tab last picked.</summary>
    internal static string WantTab;

    // A stat's list key: its place, id and count, so a move or swap builds its line and its inspector again, renumbered.
    private static string ItemKey(PanelConfig panel, StatEntry s) => ShellRows.ItemKey(s, Number(panel, s)) + "/" + s.Id + "/" + Count(panel, s);

    /// <summary>A drop in panel's stats: remove at from, insert at to, then save.</summary>
    internal static void Move(PanelConfig panel, int from, int to) {
        Dictionary<object, int> before = PanelsList.Places(Panels);
        StatEntry s = panel.Stats[from];
        panel.Stats.RemoveAt(from);
        panel.Stats.Insert(to, s);
        Changed(before);
    }

    // Fold states follow the stats' new numbers.
    private static void Changed(Dictionary<object, int> before = null) {
        PanelsOverlay.Save();
        if(before != null) PanelsList.FoldsFollow(before);
        QuartzShell.Surface?.Refresh();
    }

    private static string StatKey(string id) => Quartz.Localization.LocaleKeys.FromText("PANEL_STAT", id);

    private static string DefaultLabel(string id) {
        foreach(PanelsOverlay.StatDef stat in PanelsOverlay.AllStats)
            if(stat.Id == id) return stat.Label;
        return id;
    }

    // A stat's number across the page: the stats of the panels before, then its place.
    private static int Number(PanelConfig panel, StatEntry s) {
        int g = 0;
        foreach(PanelConfig p in Panels) {
            if(p == panel) break;
            g += p.Stats.Count;
        }
        return g + panel.Stats.IndexOf(s) + 1;
    }

    // How many times its stat is listed on the page up to it, itself counted.
    private static int Count(PanelConfig panel, StatEntry s) {
        int k = 0;
        foreach(PanelConfig p in Panels) {
            foreach(StatEntry e in p.Stats) {
                if(e.Id == s.Id) k++;
                if(e == s) return k;
            }
            if(p == panel) break;
        }
        return k;
    }

    // The selected stat's inspector: a card titled by the stat, its own rows on one tab and its Setting rows on another.
    private static void Inspector(VisualElement box, PanelConfig panel, StatEntry s, int n, Func<object> panelDocument, Action<VisualElement> swap, Action remove) {
        int g = Number(panel, s);
        Func<object> document = () => panelDocument() != null && panel.Stats.Contains(s) ? s : null;
        void Row(ControlOptions o, string key) {
            o.Id = key + "#" + g;
            o.UndoDocument = document;
        }
        VisualElement card = Controls.Card(box, QuartzShell.Label(StatKey(s.Id), DefaultLabel(s.Id)));
        card.name = "PANEL_INSPECTOR#" + g;   // where the classic page's Setting fold (SETTING_SHORT#g) stood
        int want = WantTab == null ? -1 : WantTab == "SETTING_SHORT" ? 1 : 0;
        WantTab = null;
        ShellRows.CardTabs(card, new List<NavItem> {
            new("PANEL_TAB_STAT", e => StatRows(e, s, g, Row, swap, remove)),
            new("SETTING_SHORT", e => Settings(e, s, n, g, Row)),
        }, want);
    }

    // A stat's own rows: its text or image field, Show and Label, then Swap and Delete on one row.
    private static void StatRows(VisualElement e, StatEntry s, int g, Action<ControlOptions, string> row, Action<VisualElement> swap, Action remove) {
        Action save = PanelsOverlay.Save;
        if(s.Id == "text")
            ShellRows.Text(e, "", Bind.To(() => s.Text, v => s.Text = v), 64, null, save, o => {
                row(o, "PANEL_TEXT");
                o.Placeholder = "PANEL_TEXT_PLACEHOLDER";
            });
        else if(s.Id == "image")
            ShellRows.Text(e, "", Bind.To(() => s.Image, v => s.Image = v), 260, null, save, o => {
                row(o, "PANEL_IMAGE");
                o.Placeholder = "PANEL_IMAGE_PLACEHOLDER";
            });
        Controls.Toggle(e, "PANEL_SHOWSTAT", QuartzShell.Writes(() => s.Enabled, v => {
            s.Enabled = v;
            save();
        }), o => {
            row(o, "PANEL_SHOWSTAT");
            o.Default = true;
        });
        Controls.Toggle(e, "PANEL_SHOWLABEL", QuartzShell.Writes(() => s.ShowLabel, v => {
            s.ShowLabel = v;
            save();
        }), o => row(o, "PANEL_SHOWLABEL"));
        Button swapButton = null;
        swapButton = ShellRows.Buttons(e, new ShellRows.Btn("SWAP", () => swap(swapButton), more: o => o.Id = "SWAP#" + g),
            new ShellRows.Btn("DELETE_BUTTON", remove, danger: true, more: o => o.Id = "DELETE_BUTTON#" + g))[0];
    }

    // The classic Setting rows: an image's pick, folder and size; the FPS interval; then the value's colours.
    private static void Settings(VisualElement body, StatEntry s, int n, int g, Action<ControlOptions, string> row) {
        Action save = PanelsOverlay.Save;
        if(s.Id == "image") {
            Controls.Action(body, "", "PANEL_IMAGE_CHOOSE", () => QuartzShell.Do("panels.imagepick:" + n, () => {
                string picked = PickImage();
                if(string.IsNullOrEmpty(picked)) return;
                s.Image = picked;
                save();
            }), o => {
                o.Id = "PANEL_IMAGE_CHOOSE#" + g;
                o.Tooltip = "DESC_PANEL_IMAGE_CHOOSE";
            });
            Controls.Action(body, "", "PANEL_IMAGE_FOLDER", () => QuartzShell.Do("panels.imagefolder", () => FileDialog.Reveal(PanelsOverlay.EnsureImagesDir())),
                o => o.Id = "PANEL_IMAGE_FOLDER#" + g);
            ShellRows.ReleaseSlider(body, "PANEL_IMAGE_SIZE", Bind.To(() => s.ImageScale, v => s.ImageScale = v), PanelsOverlay.MinImageScale, PanelsOverlay.MaxImageScale,
                0.05f, "0.00'x'", null, save, o => {
                    row(o, "PANEL_IMAGE_SIZE");
                    o.Default = 1f;
                    o.Tooltip = "DESC_PANEL_IMAGE_SIZE";
                });
        }
        if(s.Id == "fps")
            ShellRows.ReleaseSlider(body, "OVERLAY_FPS_SMOOTH", Bind.To(() => MainCore.Conf.FpsRefreshInterval, v => MainCore.Conf.FpsRefreshInterval = v), 0f, 2f, 0.05f,
                "0.00 s", null, () => MainCore.ConfMgr.RequestSave(), o => {
                    row(o, "OVERLAY_FPS_SMOOTH");
                    o.Default = 0f;
                    o.Tooltip = "DESC_OVERLAY_FPS_SMOOTH";
                });
        Colours(body, s, g, row);
    }

    // A stat's colour is made only when a row writes it, as the classic page makes it only when its Setting opens: until
    // then the rows show the stat's default, and the first write keeps that very object.
    private static void Colours(VisualElement body, StatEntry s, int g, Action<ControlOptions, string> row) {
        Action save = PanelsOverlay.Save;
        StatColor shown = null;
        StatColor Read() => s.Color ?? (shown ??= StatColor.DefaultFor(s.Id));
        StatColor Write() => s.Color ??= Read();
        bool ratio = StatColor.HasRatio(s.Id);
        Controls.Toggle(body, "PANEL_STATCOLOR_ON", QuartzShell.Writes(() => Read().Enabled, v => {
            Write().Enabled = v;
            save();
        }), o => {
            row(o, "PANEL_STATCOLOR_ON");
            o.Default = false;
            o.Tooltip = "DESC_PANEL_STATCOLOR_ON";
        });
        if(StatColor.IsBpm(s.Id))
            ShellRows.ReleaseSlider(body, "PANEL_STATCOLOR_MAXBPM", Bind.To(() => Read().MaxBpm, v => Write().MaxBpm = Mathf.Clamp(Mathf.Round(v), 1f, 9999f)),
                1f, 9999f, 1f, "0", null, save, o => {
                    row(o, "PANEL_STATCOLOR_MAXBPM");
                    o.Default = 8000f;
                    o.Tooltip = "DESC_PANEL_STATCOLOR_MAXBPM";
                });
        Controls.Repeat<ColorPoint>(body, () => Read().Points, p => ShellRows.ItemKey(p, Read().Points.IndexOf(p)), (box, p) => {
            string k = g + "." + (Read().Points.IndexOf(p) + 1);
            Func<object> document = () => s.Color != null && s.Color.Points.Contains(p) ? p : null;
            if(ratio) {
                ShellRows.ReleaseSlider(box, "PANEL_STATCOLOR_POS", Bind.To(() => p.Pos * 100f, v => {
                    Write();
                    p.Pos = Mathf.Clamp(Mathf.Round(v * 2f) * 0.5f, 0f, 100f) * 0.01f;
                }), 0f, 100f, 0.5f, "0.#' %'", null, () => {
                    Write().SortPoints();
                    Changed();
                }, o => {
                    o.Id = "PANEL_STATCOLOR_POS#" + k;
                    o.Default = 100f;
                    o.UndoDocument = document;
                });
            }
            ShellRows.Colour(box, "PANEL_STATCOLOR_COLOR", p.GetColor, c => {
                Write();
                p.SetColor(c);
            }, null, save, configure: o => {
                o.Id = "PANEL_STATCOLOR_COLOR#" + k;
                o.Default = Color.white;
                o.UndoDocument = document;
            });
            Controls.Action(box, "", "DELETE_BUTTON", () => {
                Write().Points.Remove(p);
                Changed();
            }, o => {
                o.Id = "PANEL_STATCOLOR_REMOVE#" + k;
                o.VisibleWhen = () => Read().Points.Count > 1;
            });
        });
        if(!ratio) return;
        Controls.Action(body, "", "PANEL_STATCOLOR_ADD", () => {
            StatColor c = Write();
            float pos = c.Points.Count > 0 ? 0.5f : 1f;
            c.Points.Add(new ColorPoint(pos, c.Evaluate(pos)));
            c.SortPoints();
            Changed();
        }, o => {
            o.Id = "PANEL_STATCOLOR_ADD#" + g;
            o.VisibleWhen = () => Read().Points.Count < 8;
        });
        Controls.Toggle(body, "PANEL_STATCOLOR_PERFECT", QuartzShell.Writes(() => Read().UsePerfect, v => {
            Write().UsePerfect = v;
            save();
        }), o => {
            row(o, "PANEL_STATCOLOR_PERFECT");
            o.Default = false;
            o.Tooltip = "DESC_PANEL_STATCOLOR_PERFECT";
        });
        ShellRows.Colour(body, "PANEL_STATCOLOR_PERFECTCOLOR", () => Read().Perfect.GetColor(), c => Write().Perfect.SetColor(c), null, save, configure: o => {
            row(o, "PANEL_STATCOLOR_PERFECTCOLOR");
            o.Default = PerfectDefault;
        });
    }

    private static string PickImage() =>
        FileDialog.PickFile(PanelsOverlay.EnsureImagesDir(), "Image", ["png", "jpg", "jpeg"], "Select panel image");
}
#endif

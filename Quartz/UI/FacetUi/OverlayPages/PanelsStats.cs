#if !IL2CPP
using Facet;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Features.Panels;
using Quartz.Overlay;
using Quartz.UI.Generator;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.OverlayPages;
/// <summary>
/// One panel's stats, as the classic page's: Add Stat opens the picker by category (Close shuts it), Clear All Stats, then
/// the stats in their order, dragged by their label, each with its text or image field, Show, Label, Swap (the picker
/// replaces it) and X, and its Setting rows folded under it. Ids of a stat's rows are numbered across the page, as the
/// classic page lists them: the stat's own label (PANEL_STAT_&lt;ID&gt;) counts that stat, the rest count every stat.
/// </summary>
internal static class PanelsStats {
    private static List<PanelConfig> Panels => PanelsOverlay.Conf.Panels;
    private static readonly string[] Categories = ["Accuracy", "Time", "BPM", "Map Stats", "Other"];
    private static readonly Color PerfectDefault = new(1f, 0.854902f, 0f, 1f);

    /// <summary>Panel n's stat rows.</summary>
    internal static void Build(VisualElement box, PanelConfig panel, int n) {
        Func<object> document = ShellRows.ItemDocument(panel, () => Panels);
        StatEntry replace = null;
        bool open = false;
        var picks = new List<string>();   // the picker's rows while open: "c:<category>", "s:<stat id>", or "none"
        Button add = null;

        void Close() {
            open = false;
            replace = null;
            picks.Clear();
            if(add != null) Words.Label(add, "PANEL_ADDSTAT");
        }
        void Open() {
            open = true;
            Fill(panel, picks);
            if(add != null) Words.Label(add, "CLOSE");
            QuartzShell.Surface?.Refresh();
        }
        void Pick(string id) {
            if(replace != null) replace.Id = id;
            else panel.Stats.Add(new StatEntry(id) { ShowLabel = id is not ("text" or "image") });
            Close();
            Changed();
        }

        add = Controls.Action(box, "", "PANEL_ADDSTAT", () => {
            if(open) Close();
            else {
                replace = null;
                Open();
            }
        }, o => {
            o.Id = "PANEL_ADDSTAT#" + n;
            o.Tooltip = "DESC_PANEL_ADDSTAT";
        });
        Controls.Action(box, "", "PANEL_CLEARSTATS", () => {
            panel.Stats.Clear();
            Close();
            Changed();
        }, o => o.Id = "PANEL_CLEARSTATS#" + n);
        Controls.Repeat<string>(box, () => picks, k => k, (row, k) => PickRow(row, k, n, Pick));
        Controls.Info(box, "PANEL_NO_STATS", () => "", (InfoOptions o) => {
            o.Id = "PANEL_NO_STATS#" + n;
            o.VisibleWhen = () => panel.Stats.Count == 0;
        });
        Controls.Reorder<StatEntry>(box, o => {
            o.Id = "PANEL_STATS#" + n;
            o.Vertical = true;
            o.DragFromText = true;
            o.Items = () => panel.Stats;
            o.Key = s => ShellRows.ItemKey(s, Number(panel, s)) + "/" + s.Id + "/" + Count(panel, s);
            o.Text = s => QuartzHost.Text(StatKey(s.Id), DefaultLabel(s.Id));
            o.Move = (from, to) => Move(panel, from, to);
            o.Build = (e, s) => Stat(e, panel, s, n, document, () => {
                replace = s;
                Open();
            }, () => {
                panel.Stats.Remove(s);
                if(replace == s) Close();   // a pick would swap the removed stat, and be lost
                Changed();
            });
        });
    }

    /// <summary>A drop in panel's stats: remove at from, insert at to, then save.</summary>
    internal static void Move(PanelConfig panel, int from, int to) {
        StatEntry s = panel.Stats[from];
        panel.Stats.RemoveAt(from);
        panel.Stats.Insert(to, s);
        Changed();
    }

    // The classic page shows the stats too: it rebuilds before it next shows.
    private static void Changed() {
        PanelsOverlay.Save();
        UICore.ClassicStale = true;
        QuartzShell.Surface?.Refresh();
    }

    // The picker as the classic page builds it: the five categories, then any an addon's stats bring, each with its stats not
    // on the panel yet (text and image can be added any number of times).
    private static void Fill(PanelConfig panel, List<string> picks) {
        picks.Clear();
        IReadOnlyList<PanelsOverlay.StatDef> all = PanelsOverlay.AllStats;
        List<string> categories = [.. Categories];
        foreach(PanelsOverlay.StatDef stat in all)
            if(!categories.Contains(stat.Category)) categories.Add(stat.Category);
        foreach(string category in categories) {
            bool header = false;
            foreach(PanelsOverlay.StatDef stat in all) {
                if(stat.Category != category) continue;
                if(stat.Id is not ("text" or "image") && panel.Stats.Exists(e => e.Id == stat.Id)) continue;
                if(!header) picks.Add("c:" + category);
                header = true;
                picks.Add("s:" + stat.Id);
            }
        }
        if(picks.Count == 0) picks.Add("none");
    }

    private static void PickRow(VisualElement row, string k, int n, Action<string> pick) {
        if(k == "none") {
            Ui.Muted(row, "PANEL_ALL_STATS_ADDED").name = "PANEL_ALL_STATS_ADDED#" + n;
            return;
        }
        string what = k[2..];
        if(k[0] == 'c') {
            Ui.Muted(row, QuartzShell.Label(GenerateUI.LocaleKeyFromText("PANEL_CATEGORY", what), what));
            return;
        }
        Controls.Action(row, "", QuartzShell.Label(StatKey(what), DefaultLabel(what)), () => pick(what),
            o => o.Id = GenerateUI.LocaleKeyFromText("PANEL_PICK", what) + "#" + n);
    }

    private static string StatKey(string id) => GenerateUI.LocaleKeyFromText("PANEL_STAT", id);

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

    // One stat: under its label, its text or image field, Show and Label, Swap and X, then its Setting rows, folded.
    private static void Stat(VisualElement e, PanelConfig panel, StatEntry s, int n, Func<object> panelDocument, Action swap, Action remove) {
        int g = Number(panel, s);
        if(s.Id is not ("text" or "image") && e.Q<Label>("PANEL_STATS#" + n + "-text") is Label label) label.name = StatKey(s.Id) + "#" + Count(panel, s);
        Func<object> document = () => panelDocument() != null && panel.Stats.Contains(s) ? s : null;
        Action save = PanelsOverlay.Save;
        void Row(ControlOptions o, string key) {
            o.Id = key + "#" + g;
            o.UndoDocument = document;
        }
        if(s.Id == "text")
            ShellRows.Text(e, "", Bind.To(() => s.Text, v => s.Text = v), 64, null, save, o => {
                Row(o, "PANEL_TEXT");
                o.Placeholder = "PANEL_TEXT_PLACEHOLDER";
            });
        else if(s.Id == "image")
            ShellRows.Text(e, "", Bind.To(() => s.Image, v => s.Image = v), 260, null, save, o => {
                Row(o, "PANEL_IMAGE");
                o.Placeholder = "PANEL_IMAGE_PLACEHOLDER";
            });
        Controls.Toggle(e, "PANEL_SHOWSTAT", QuartzShell.Writes(() => s.Enabled, v => {
            s.Enabled = v;
            save();
        }), o => {
            Row(o, "PANEL_SHOWSTAT");
            o.Default = true;
        });
        Controls.Toggle(e, "PANEL_SHOWLABEL", QuartzShell.Writes(() => s.ShowLabel, v => {
            s.ShowLabel = v;
            save();
        }), o => Row(o, "PANEL_SHOWLABEL"));
        Controls.Action(e, "", "SWAP", swap, o => o.Id = "SWAP#" + g);
        Controls.Action(e, "", "DELETE_SHORT", remove, o => o.Id = "DELETE_SHORT#" + g);
        Settings(Controls.Section(e, "SETTING_SHORT", o => {
            o.Open = false;
            o.Id = "SETTING_SHORT#" + g;
        }), s, n, g, Row);
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
                UICore.ClassicStale = true;
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
            Controls.Action(box, "", "DELETE_SHORT", () => {
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

    private static string PickImage() {
        try {
            return FileDialog.PickFile(PanelsOverlay.EnsureImagesDir(), "Image", ["png", "jpg", "jpeg"], "Select panel image");
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(PanelsStats)}] panel image PickFile failed: {e}");
            return null;
        }
    }
}
#endif

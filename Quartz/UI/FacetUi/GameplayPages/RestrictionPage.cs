#if !IL2CPP
using Facet;
using Quartz.Features.Interop;
using Quartz.Features.Restriction;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.GameplayPages;
/// <summary>Gameplay ▸ Judgement Restriction in the new menu, row for row as PageRestriction. A stored X-Perfect-only mode
/// without XPerfect shows as Pure Perfect and stays stored until a pick. Each section's start and end share one range; a step
/// on a section that went away is skipped.</summary>
internal static class RestrictionPage {
    private static RestrictionSettings C => Restriction.Conf;
    private static readonly RestrictionSettings Def = new();

    /// <summary>The allow tiles in the classic page's order; no Auto tile and no X Perfect variant.</summary>
    internal static readonly JudgementTiles.Row[] Rows = [
        new("JR_ALLOW_TOOEARLY", "jr_allow_tooearly", 0),
        new("JR_ALLOW_VERYEARLY", "jr_allow_veryearly", 1),
        new("JR_ALLOW_EARLYPERFECT", "jr_allow_earlyperfect", 2),
        new("JR_ALLOW_PERFECT", "jr_allow_perfect", 3),
        new("JR_ALLOW_LATEPERFECT", "jr_allow_lateperfect", 4),
        new("JR_ALLOW_VERYLATE", "jr_allow_verylate", 5),
        new("JR_ALLOW_TOOLATE", "jr_allow_toolate", 6),
        new("JR_ALLOW_MULTIPRESS", "jr_allow_multipress", 7),
        new("JR_ALLOW_MISS", "jr_allow_miss", 8),
        new("JR_ALLOW_OVERLOAD_NOFAIL", "jr_allow_overload_nofail", 9),
        new("JR_ALLOW_OVERLOAD_FAIL", "jr_allow_overload_fail", 11),
    ];

    internal static void Build(VisualElement p) {
        Restriction.EnsureConf();
        ShellPage.Feature(p, "restriction", "SECTION_JUDGEMENT_RESTRICTION", "RESTRICTION_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        Action save = Restriction.Save;
        VisualElement mode = Controls.Card(p, null);
        Controls.Dropdown(mode, "JR_MODE_LABEL", QuartzShell.Writes(() => PageRules.ShownMode(C.JRestrictMode, XPerfectBridge.Installed), v => {
            C.JRestrictMode = v;
            save();
        }), PageRules.RestrictModes(XPerfectBridge.Installed), m => m switch {
            0 => "JR_MODE_MIN_ACCURACY",
            1 => "JR_MODE_PURE_PERFECT",
            2 => "JR_MODE_XPURE_PERFECT",
            3 => "JR_MODE_CUSTOM",
            _ => "JR_MODE_NO_TOO_EARLY",
        }, o => {
            o.Id = "JR_MODE_LABEL";
            o.Default = Def.JRestrictMode;
        });
        ShellRows.ReleaseSlider(mode, "JR_ACC", Bind.To(() => C.JRestrictAccuracy, v => C.JRestrictAccuracy = v), 0f, 100f, 0f, "0.0", null, save, o => {
            o.Default = Def.JRestrictAccuracy;
            o.VisibleWhen = () => C.JRestrictMode == 0;
        });
        JudgementTiles.Grid(p, "JR_MODE_CUSTOM", Bind.To(() => C.JRestrictAllowedMask, v => C.JRestrictAllowedMask = v), Def.JRestrictAllowedMask, Rows,
            save, o => o.VisibleWhen = () => C.JRestrictMode == 3);

        VisualElement card = Controls.Card(p, null);
        Controls.Hook(Controls.Header(card, "JR_SECTIONS"), o => o.Id = "JR_SECTIONS");
        ShellRows.Toggle(card, "JR_SECTIONS_ON", "jr_sections_on", Bind.To(() => C.JRestrictSectionsEnabled, v => C.JRestrictSectionsEnabled = v), null, save, o => {
            o.Default = Def.JRestrictSectionsEnabled;
            o.Tooltip = "DESC_JR_SECTIONS_ON";
        });
        VisualElement region = Controls.When(card, () => C.JRestrictSectionsEnabled);
        Controls.Info(region, "JR_NO_SECTIONS", () => "", (InfoOptions o) => {
            o.Id = "JR_NO_SECTIONS";
            o.VisibleWhen = () => C.JRestrictSections.Count == 0;
        });
        Controls.Repeat<JudgementSection>(region, () => C.JRestrictSections, s => ShellRows.ItemKey(s, C.JRestrictSections.IndexOf(s)), Section);
        Controls.Action(region, "", "JR_SECTIONS_ADD", () => {
            if(C.JRestrictSections.Count >= RestrictionSettings.MaxSections) return;
            C.JRestrictSections.Add(new JudgementSection());
            save();
        }, o => {
            o.Id = "JR_SECTIONS_ADD";
            o.Tooltip = "DESC_JR_SECTIONS_ADD";
            o.EnabledWhen = () => C.JRestrictSections.Count < RestrictionSettings.MaxSections;
        });
        Controls.AfterChange(region, save);
        ShellRows.Text(card, "JR_MESSAGE", Bind.To(() => C.JRestrictMessage, v => C.JRestrictMessage = v), 0, null, save, o => {
            o.Default = Def.JRestrictMessage;
            o.Tooltip = "DESC_JR_MESSAGE";
        });
        Controls.Info(card, "JR_MESSAGE_HINT", () => "", (InfoOptions o) => o.Id = "JR_MESSAGE_HINT");
    }

    // One section, shut, titled and numbered by its place: its range, then Remove.
    private static void Section(VisualElement box, JudgementSection s) {
        int n = C.JRestrictSections.IndexOf(s) + 1;
        VisualElement body = Controls.Section(box, string.Format(QuartzHost.Text("JR_SECTION", "Section {0}"), n), o => {
            o.Open = false;
            o.Id = "JR_SECTION#" + n;
        });
        Controls.Range(body, "JRSEC_START", "JRSEC_END", QuartzShell.Writes(() => new Vector2(s.Start, s.End), v => {
            s.Start = Percent(v.x);
            s.End = Percent(v.y);
        }), o => {
            o.Id = "JRSEC_START#" + n;
            o.HighId = "JRSEC_END#" + n;
            o.Min = 0f;
            o.Max = 100f;
            o.Step = 0.1f;
            o.Format = "0.0' %'";
            o.Default = new Vector2(0f, 100f);
            o.UndoDocument = ShellRows.ItemDocument(s, () => C.JRestrictSections);
        });
        Controls.Action(body, "", "JRSEC_REMOVE", () => {
            C.JRestrictSections.Remove(s);
            Restriction.Save();
        }, o => o.Id = "JRSEC_REMOVE#" + n);
    }

    private static float Percent(float v) => Mathf.Clamp(Mathf.Round(v * 10f) / 10f, 0f, 100f);
}
#endif

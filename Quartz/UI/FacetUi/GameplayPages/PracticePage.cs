#if !IL2CPP
using Facet;
using Quartz.Features.Practice;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.GameplayPages;
/// <summary>Gameplay ▸ Practice Difficulty in the new menu, row for row as PagePractice, beside a live preview of the readout.
/// Each binding is a shut section numbered by its place; a step on a binding that went away is skipped.</summary>
internal static class PracticePage {
    private static PracticeSettings C => PracticeDifficulty.Conf;
    private static readonly PracticeSettings Def = new();
    private static readonly PracticeBinding DefBinding = new();
    private static readonly int[] Difficulties = [0, 1, 2];

    internal static void Build(VisualElement p) {
        PracticeDifficulty.EnsureConf();
        ShellPage.Feature(p, "practice", "SECTION_PRACTICE_DIFFICULTY", "PRACTICE_HINT", PracticeOverlay.FontKey, Cards);
        ShellPage.Described(p, "SECTION_PRACTICE_DIFFICULTY", "PRACTICE_HINT");
    }

    private static void Cards(VisualElement p) {
        Action save = PracticeDifficulty.Save, apply = PracticeOverlay.Apply;
        VisualElement card = Controls.Card(p, null);
        ShellRows.Font(card, PracticeOverlay.FontKey);
        Controls.Info(card, "PRACTICE_NO_BINDINGS", () => "", (InfoOptions o) => {
            o.Id = "PRACTICE_NO_BINDINGS";
            o.VisibleWhen = () => C.Bindings.Count == 0;
        });
        Controls.Repeat<PracticeBinding>(card, () => C.Bindings, b => ShellRows.ItemKey(b, C.Bindings.IndexOf(b)), Binding);
        Controls.Action(card, "", "PRACTICE_ADD", () => {
            if(C.Bindings.Count >= 16) return;
            C.Bindings.Add(new PracticeBinding());
            save();
            QuartzShell.Changed();   // the search index finds the new binding's rows
        }, o => {
            o.Id = "PRACTICE_ADD";
            o.Tooltip = "DESC_PRACTICE_ADD";
            o.EnabledWhen = () => C.Bindings.Count < 16;
        });

        VisualElement readout = Controls.Card(p, "PRACTICE_INDICATOR", o => o.Id = "PRACTICE_INDICATOR");
        ShellRows.Toggle(readout, "PRACTICE_SHOWINDICATOR", "practice_showindicator", Bind.To(() => C.ShowIndicator, v => C.ShowIndicator = v), null, save, o => {
            o.Default = Def.ShowIndicator;
            o.Tooltip = "DESC_PRACTICE_SHOWINDICATOR";
        });
        ShellRows.Toggle(readout, "PRACTICE_SHOWSPEED", "practice_showspeed", Bind.To(() => C.ShowSpeed, v => C.ShowSpeed = v), null, save, o => {
            o.Default = Def.ShowSpeed;
            o.Tooltip = "DESC_PRACTICE_SHOWSPEED";
        });
        ShellRows.Toggle(readout, "PRACTICE_INGAMEONLY", "practice_ingameonly", Bind.To(() => C.IndicatorOnlyInGame, v => C.IndicatorOnlyInGame = v), null, save,
            o => o.Default = Def.IndicatorOnlyInGame);
        ShellRows.ReleaseSlider(readout, "PRACTICE_FONTSIZE", Bind.To(() => C.FontSize, v => C.FontSize = v), 8f, 120f, 1f, "0 px", apply, save,
            o => o.Default = Def.FontSize);
        ShellRows.Colour(readout, "PRACTICE_COLOR", () => C.GetColor(), c => C.SetColor(c), apply, save, configure: o => o.Default = Def.GetColor());
        ShellRows.ResetPosition(readout, "PRACTICE_RESETPOS", PracticeOverlay.ResetPosition);
    }

    // One binding: its key, difficulty and pitch, what it applies, then Remove. Every row's steps go stale with the binding.
    private static void Binding(VisualElement box, PracticeBinding b) {
        int i = C.Bindings.IndexOf(b), n = i + 1;
        Action save = PracticeDifficulty.Save;
        Func<object> document = ShellRows.ItemDocument(b, () => C.Bindings);
        VisualElement body = ShellRows.Section(box, string.Format(QuartzHost.Text("PRACTICE_BINDING", "Key {0}"), n), o => {
            o.Open = false;
            o.Id = "PRACTICE_BINDING#" + n;
        });
        ShellRows.Key(body, "PRACTICE_KEY", () => b.Key, k => b.Key = k, save,
            name: k => k == KeyCode.None ? QuartzHost.Text("PRACTICE_UNBOUND", "Click to bind a key") : k.ToString(), accept: k => k != KeyCode.None, configure: o => {
                o.Id = "PRACTICE_KEY#" + n;
                o.Tooltip = "DESC_PRACTICE_KEY";
                o.UndoDocument = document;
            });
        Controls.Dropdown(body, "PRACTICE_DIFFICULTY_LABEL", QuartzShell.Writes(() => b.Difficulty, v => {
            b.Difficulty = v;
            save();
        }), Difficulties, d => d switch {
            0 => "PRACTICE_DIFF_LENIENT",
            1 => "PRACTICE_DIFF_NORMAL",
            _ => "PRACTICE_DIFF_STRICT",
        }, o => {
            o.Id = "PRACTICE_DIFFICULTY_LABEL#" + n;
            o.Default = DefBinding.Difficulty;
            o.UndoDocument = document;
        });
        ShellRows.ReleaseSlider(body, "PRACTICE_SPEED", Bind.To(() => (float)b.Pitch, v => b.Pitch = PracticeSettings.ClampPitch(Mathf.RoundToInt(v))),
            PracticeSettings.MinPitch, PracticeSettings.MaxPitch, 1f, "0 %", null, save, o => {
                o.Id = "PRACTICE_SPEED#" + n;
                o.Default = DefBinding.Pitch;
                o.UndoDocument = document;
            });
        ShellRows.Toggle(body, "PRACTICE_SETSDIFF", $"practice{i}_setsdiff", Bind.To(() => b.SetsDifficulty, v => b.SetsDifficulty = v), null, save, o => {
            o.Id = "PRACTICE_SETSDIFF#" + n;
            o.Default = DefBinding.SetsDifficulty;
            o.UndoDocument = document;
        });
        ShellRows.Toggle(body, "PRACTICE_SETSSPEED", $"practice{i}_setsspeed", Bind.To(() => b.SetsSpeed, v => b.SetsSpeed = v), null, save, o => {
            o.Id = "PRACTICE_SETSSPEED#" + n;
            o.Default = DefBinding.SetsSpeed;
            o.UndoDocument = document;
        });
        Controls.Action(body, "", "PRACTICE_REMOVE", () => {
            C.Bindings.Remove(b);
            save();
            QuartzShell.Changed();
        }, o => o.Id = "PRACTICE_REMOVE#" + n);
    }
}
#endif

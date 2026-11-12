#if !IL2CPP
using Facet;
using Quartz.Features.Calibration;
using UnityEngine;
using UnityEngine.UIElements;
using Cal = Quartz.Features.Calibration.Calibration;
namespace Quartz.UI.FacetUi.OverlayPages;
/// <summary>
/// Overlay ▸ Calibration in the new menu, row for row as PageCalibration, beside a live preview of the offset popup with a
/// sample suggestion. The rows only save: the popup and the game's calibration screen read them when they show.
/// </summary>
internal static class Calibration {
    // Read on every call: a profile switch may replace what Calibration.Conf returns.
    private static CalibrationSettings C => Cal.Conf;
    private static readonly CalibrationSettings Def = new();
    // The minimum the last switch-off cleared.
    private static int cleared;
#if DEBUG
    /// <summary>The self-test's copy of the timings the page last listed, taken as it read them: the logger's lists grow as runs end.</summary>
    internal static (float[] Map, float[] All) Listed { get; private set; } = ([], []);
#endif

    internal static void Page(VisualElement p) {
        Cal.EnsureConf();
        ShellPage.Feature(p, "calibration", "SECTION_CALIBRATION", "CALIBRATION_DESC", CalibrationPopupUI.PreviewId, Cards);
    }

    private static void Cards(VisualElement p) {
        Action save = Cal.Save;
        VisualElement main = Controls.Card(p, "SECTION_CALIBRATION");
        ShellRows.Toggle(main, "CALIBRATION_POPUP_ON_DEATH", "calibration_popup_on_death", Bind.To(() => C.ShowPopupOnDeath, v => C.ShowPopupOnDeath = v),
            null, save, o => {
                o.Default = Def.ShowPopupOnDeath;
                o.Tooltip = "DESC_CALIBRATION_POPUP_ON_DEATH";
            });
        ShellRows.Toggle(main, "CALIBRATION_DETAILED_DISPLAY", "calibration_detailed_display", Bind.To(() => C.DetailedDisplay, v => C.DetailedDisplay = v),
            null, save, o => {
                o.Default = Def.DetailedDisplay;
                o.Tooltip = "DESC_CALIBRATION_DETAILED_DISPLAY";
            });
        // The preview's sample shows whole or decimal milliseconds.
        ShellRows.Toggle(main, "CALIBRATION_FLOAT_OFFSET", "calibration_float_offset", Bind.To(() => C.FloatOffsetEnabled, v => C.FloatOffsetEnabled = v),
            CalibrationPopupUI.RefreshPreview, save, o => {
                o.Default = Def.FloatOffsetEnabled;
                o.Tooltip = "DESC_CALIBRATION_FLOAT_OFFSET";
            });

        VisualElement song = Controls.Card(p, "HEADING_CALIBRATION_SONG");
        ShellRows.Slider(song, "CALIBRATION_SONG_PITCH", Bind.To(() => C.SongPitch, v => C.SongPitch = v), 1f, 500f, 0.5f, "0.#' %'", null, save,
            o => o.Default = Def.SongPitch);
        Controls.IntSlider(song, "CALIBRATION_SONG_REPEAT", QuartzShell.Writes(() => C.SongRepeat, v => {
            C.SongRepeat = v;
            save();
        }), o => {
            o.Id = "CALIBRATION_SONG_REPEAT";
            o.Min = 0;
            o.Max = 20;
            o.Default = Def.SongRepeat;
        });
        // Off clears the minimum, as the classic switch does; undoing the off brings the cleared minimum back, which History
        // never recorded. The minimum's row follows at once: Facet re-reads rows every 100 ms.
        ShellRows.Toggle(song, "CALIBRATION_SONG_USE_MINIMUM", "calibration_song_use_minimum", Bind.To(() => C.SongUseMinimum, v => {
            C.SongUseMinimum = v;
            if(!v) {
                cleared = C.SongMinimum;
                C.SongMinimum = 0;
            } else if(Facet.History.Applying && C.SongMinimum == 0) C.SongMinimum = cleared;
        }), () => QuartzShell.Surface?.Refresh(), save, o => {
            o.Default = Def.SongUseMinimum;
            o.Tooltip = "DESC_CALIBRATION_SONG_USE_MINIMUM";
        });
        // Whole milliseconds in steps of 10, as the classic slider snaps them.
        ShellRows.Slider(song, "CALIBRATION_SONG_MINIMUM", Bind.To<float>(() => C.SongMinimum, v => C.SongMinimum = Mathf.RoundToInt(v)), 0f, 2000f, 10f, "0 ms",
            null, save, o => {
                o.Default = Def.SongMinimum;
                o.VisibleWhen = () => C.SongUseMinimum;
            });

        // Read as the page is built, as the classic page reads it: a run that ends later shows on the next visit.
        VisualElement history = Controls.Card(p, "SECTION_CALIBRATION_TIMING_HISTORY");
#if DEBUG
        Listed = ([.. CalibrationTimingLogger.RecentForCurrentMap()], [.. CalibrationTimingLogger.RecentAll()]);
        (IReadOnlyList<float> map, IReadOnlyList<float> all) = Listed;
#else
        IReadOnlyList<float> map = CalibrationTimingLogger.RecentForCurrentMap(), all = CalibrationTimingLogger.RecentAll();
#endif
        int n = 0;
        History(history, "CALIBRATION_TIMING_CURRENT_MAP", map, ref n);
        History(history, "CALIBRATION_TIMING_ALL_MAPS", all, ref n);

        ShellRows.ResetPosition(Controls.Card(p, "MENU_LAYOUT"), "CALIBRATION_RESETPOS", CalibrationPopupUI.ResetPosition);
    }

    // A caption, then each timing newest first, whose Apply sets the game's input offset to it; or a note that there are none.
    // A timing's label is its value, so its Id carries a running number instead. The caption and the note sit straight in the
    // card, which pads only its rows: they take the rows' inset themselves, so their text starts where the row labels do.
    private static void History(VisualElement card, string captionKey, IReadOnlyList<float> timings, ref int n) {
        Sizes sz = Theme.For(card).Sizes;
        Inset(Controls.Header(card, captionKey), sz);
        if(timings.Count == 0) {
            Inset(Ui.Muted("CALIBRATION_TIMING_NONE", card), sz).style.marginBottom = sz.Gap;
            return;
        }
        for(int i = timings.Count - 1; i >= 0; i--) {
            float value = timings[i];
            string id = "CALIBRATION_TIMING_APPLY-" + n++;
            Controls.Action(card, Cal.FormatMs(value) + "ms", "CALIBRATION_TIMING_APPLY", () => Cal.SetOffsetMs(value), o => o.Id = id);
        }
    }

    private static VisualElement Inset(VisualElement e, Sizes sz) {
        e.style.paddingLeft = sz.Pad;
        e.style.paddingRight = sz.Pad;
        return e;
    }
}
#endif

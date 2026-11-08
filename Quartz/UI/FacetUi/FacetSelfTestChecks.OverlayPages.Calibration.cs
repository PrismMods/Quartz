#if DEBUG && !IL2CPP
using System.Collections;
using System.Text.RegularExpressions;
using Facet;
using Quartz.Features.Calibration;
using Quartz.Overlay;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using Cal = Quartz.Features.Calibration.Calibration;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Overlay ▸ Calibration in the new menu: its rows against the classic page's, its two columns' edges, the popup switch seen
/// by the classic page, Use Minimum Offset hiding and clearing the minimum, the timing history, and the preview pane, whose
/// popup shows a sample suggestion that Decimal Offset writes again, which a real suggestion hiding under it never leaves blank
/// without the note, and which hides the popup again and makes no EventSystem.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string CalibrationPage = "overlay.calibration";
    private static CalibrationSettings CalConf => Cal.Conf;

    // Every settings row of the classic page, from Show Popup on Death to Reset Popup Position.
    private static readonly string[] CalibrationRows = [
        "CALIBRATION_POPUP_ON_DEATH", "CALIBRATION_DETAILED_DISPLAY", "CALIBRATION_FLOAT_OFFSET", "CALIBRATION_SONG_PITCH", "CALIBRATION_SONG_REPEAT",
        "CALIBRATION_SONG_USE_MINIMUM", "CALIBRATION_SONG_MINIMUM", "CALIBRATION_RESETPOS",
    ];

    private static IEnumerator OverlayCalibrationPage(Action<string, bool, string> check, Action<string> info) {
        Cal.EnsureConf();
        (bool use, int min, bool dec) = (CalConf.SongUseMinimum, CalConf.SongMinimum, CalConf.FloatOffsetEnabled);
        yield return OnPage("calibration", CalibrationPage, () => CalConf.SongUseMinimum = true,   // the minimum's row shows for the parity check
            () => {
                (CalConf.SongUseMinimum, CalConf.SongMinimum, CalConf.FloatOffsetEnabled) = (use, min, dec);
                Cal.Save();
                UICore.ClassicStale = true;
                ToggleBinds.Show("calibration_song_use_minimum", use);
                ToggleBinds.Show("calibration_float_offset", dec);
            }, () => CalibrationChecks(check, info));
    }

    private static IEnumerator CalibrationChecks(Action<string, bool, string> check, Action<string> info) {
        CalibrationSource(check, info);   // first frame: the page's lists are still the logger's
        yield return Parity(check, CalibrationPage, CalibrationRows);
        yield return ColumnEdges(check, CalibrationPage, CalibrationPopupUI.PreviewId);
        yield return ClassicSees(check, CalibrationPage, "CALIBRATION_POPUP_ON_DEATH", Bind.To(() => CalConf.ShowPopupOnDeath, v => {
            CalConf.ShowPopupOnDeath = v;
            Cal.Save();
        }));
        yield return CalibrationMinimum(check);
        CalibrationHistory(check);
        yield return CalibrationDismissed(check, info);
        // Through Decimal Offset's own switch: the sample suggestion is written again, read off the popup's message.
        bool dec = CalConf.FloatOffsetEnabled;
        yield return PreviewChecks(check, info, CalibrationPage, CalibrationPopupUI.PreviewId, () => {
            if(Body.Q(name: "CALIBRATION_FLOAT_OFFSET-input") is { } track) Click(track);
            return CalConf.FloatOffsetEnabled != dec;
        }, () => CalConf.FloatOffsetEnabled != dec && Message() == CalibrationPopupUI.Sample());
        yield return PopupHidden(check, info);
        NoEventSystem(check);
    }

    private static string Message() =>
        OverlayPreview.ShownOf(CalibrationPopupUI.PreviewId)?.transform.Find("Message")?.GetComponent<TextMeshProUGUI>()?.text;

    // The pane gave its lease back as it hid, which ran the preview's end. Checked only outside a real suggestion, when the popup rests hidden.
    private static IEnumerator PopupHidden(Action<string, bool, string> check, Action<string> info) {
        if(CalibrationPopupUI.Suggesting) {
            info($"{CalibrationPage}: a real suggestion is up, so the popup's hiding is not checked");
            yield break;
        }
        bool Hidden() => OverlayPreview.ShownOf(CalibrationPopupUI.PreviewId) is not { } popup || !popup.activeSelf;
        int frames = 0;
        for(; frames < 3 && !Hidden(); frames++) yield return null;
        GameObject shown = OverlayPreview.ShownOf(CalibrationPopupUI.PreviewId);
        check($"{CalibrationPage}: once the pane hides, the popup is hidden again within 3 frames", Hidden(),
            $"popup={shown != null} active={shown != null && shown.activeSelf} suggesting={CalibrationPopupUI.Suggesting} frames={frames}");
    }

    // The popup makes its own EventSystem only while none is current, and the game's is: the scene's are switched off for one
    // lease taken and given back, then on again in the same frame, the same one current.
    private static void NoEventSystem(Action<string, bool, string> check) {
        EventSystem was = EventSystem.current;
        GameObject selected = was?.currentSelectedGameObject;
        List<EventSystem> on = [.. UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Where(e => e.isActiveAndEnabled)];
        int before = EventSystems(), after = -1;
        bool none = false, taken = false;
        try {
            foreach(EventSystem e in on) e.enabled = false;
            none = EventSystem.current == null;
            OverlayPreview.Lease lease = OverlayPreview.Take(CalibrationPopupUI.PreviewId);
            taken = lease != null;
            lease?.Release();
            after = EventSystems();
        } finally {
            foreach(EventSystem e in on) if(e != null) e.enabled = true;
            if(was != null) {
                EventSystem.current = was;
                was.SetSelectedGameObject(selected);
            }
        }
        check($"{CalibrationPage}: a preview begun and ended while no EventSystem is current makes none",
            none && taken && after == before && GameObject.Find("QuartzCalibrationEventSystem") == null,
            $"switchedOff={on.Count} current={!none} taken={taken} before={before} after={after}");
    }

    // A real suggestion shown and hidden under the pane: once it hides there is nothing to frame, so the note shows, and the
    // pane then borrows the popup again with the sample. The stage is never left blank without the note.
    private static IEnumerator CalibrationDismissed(Action<string, bool, string> check, Action<string> info) {
        string id = CalibrationPopupUI.PreviewId;
        VisualElement note = Body.Q(name: id + "-preview-empty");
        for(int i = 0; i < 10 && !OverlayPreview.Is(id); i++) yield return null;
        if(CalibrationPopupUI.Suggesting) {
            info($"{CalibrationPage}: a real suggestion is up, so none is shown and hidden under the pane");
            yield break;
        }
        bool leased = OverlayPreview.Is(id), cursor = UnityEngine.Cursor.visible, noted = false;
        bool Sampled() => OverlayPreview.Is(id) && OverlayPreview.ShownOf(id) is { activeSelf: true } && Message() == CalibrationPopupUI.Sample();
        float t0 = Time.realtimeSinceStartup;
        try {
            float cur = Cal.GetOffsetMs();
            CalibrationPopupUI.Show(cur, cur + 0.01f);   // not the sample's 12.4; a stray Yes then leaves the offset as it is
            CalibrationPopupUI.Hide();
            while(Time.realtimeSinceStartup - t0 < 1f && !(noted && Sampled())) {   // the hide runs 0.2 s
                yield return null;
                noted |= note != null && Displayed(note);
            }
        } finally {
            UnityEngine.Cursor.visible = cursor;
        }
        check($"{CalibrationPage}: a real suggestion hidden under the pane shows the note, then the sample within 1 s",
            leased && note != null && noted && Sampled(),
            $"leased={leased} note={note != null} noted={noted} sampled={Sampled()} target={OverlayPreview.Target} "
            + $"active={OverlayPreview.ShownOf(id)?.activeSelf} message='{Message()}' s={Time.realtimeSinceStartup - t0:0.00}");
    }

    private static int EventSystems() => UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

    // Use Minimum Offset switched off through its row hides the minimum's row within 3 frames and clears the minimum, as the
    // classic switch does. Both are put back.
    private static IEnumerator CalibrationMinimum(Action<string, bool, string> check) {
        (bool use, int min) = (CalConf.SongUseMinimum, CalConf.SongMinimum);
        VisualElement track = Body.Q(name: "CALIBRATION_SONG_USE_MINIMUM-input"), row = Body.Q(name: "CALIBRATION_SONG_MINIMUM");
        bool shown = row != null && Displayed(row);
        int frames = 0;
        try {
            CalConf.SongMinimum = 100;   // something to clear
            if(track != null && CalConf.SongUseMinimum) Click(track);
            for(; frames < 3 && row != null && Displayed(row); frames++) yield return null;
            check($"{CalibrationPage}: Use Minimum Offset switched off through its row hides the minimum's row within 3 frames and sets the minimum to 0",
                track != null && shown && !Displayed(row) && !CalConf.SongUseMinimum && CalConf.SongMinimum == 0,
                $"row={row != null} track={track != null} shownBefore={shown} frames={frames} on={CalConf.SongUseMinimum} minimum={CalConf.SongMinimum}");
        } finally {
            (CalConf.SongUseMinimum, CalConf.SongMinimum) = (use, min);
            Cal.Save();
            UICore.ClassicStale = true;
            ToggleBinds.Show("calibration_song_use_minimum", use);
        }
        yield return Frames(2);
    }

    // The page lists the logger's lists, not some other: its snapshot against fresh reads. A run that ended since the page was
    // built changes the counts, and that is not the page's fault.
    private static void CalibrationSource(Action<string, bool, string> check, Action<string> info) {
        (IReadOnlyList<float> map, IReadOnlyList<float> all) = OverlayPages.Calibration.Listed;
        IReadOnlyList<float> freshMap = CalibrationTimingLogger.RecentForCurrentMap(), freshAll = CalibrationTimingLogger.RecentAll();
        string counts = $"map={map.Count}/{freshMap.Count} all={all.Count}/{freshAll.Count}";
        if(map.Count != freshMap.Count || all.Count != freshAll.Count) {
            info($"{CalibrationPage}: the logger's counts changed since the page was built, so its source is not compared ({counts})");
            return;
        }
        check($"{CalibrationPage}: the page lists the timing logger's current-map and all-maps lists", map.SequenceEqual(freshMap) && all.SequenceEqual(freshAll), counts);
    }

    private static readonly Regex TimingRow = new("^CALIBRATION_TIMING_APPLY-[0-9]+$");

    // The timing history as the classic page lists it: each timing of the current map, then of all maps, newest first, under
    // its caption with Apply; a note where a list is empty. Nothing is applied: Apply writes the game's input offset.
    // Against what the page read as it was built: a run may have ended since.
    private static void CalibrationHistory(Action<string, bool, string> check) {
        (IReadOnlyList<float> map, IReadOnlyList<float> all) = OverlayPages.Calibration.Listed;
        List<string> want = [.. map.Reverse().Concat(all.Reverse()).Select(v => Cal.FormatMs(v) + "ms")];
        List<VisualElement> rows = Body.Query<VisualElement>().Where(e => e.name != null && TimingRow.IsMatch(e.name)).ToList();
        List<string> got = [.. rows.Select(r => r.Q<Label>()?.text)], ids = [.. rows.Select(r => r.name)];
        bool numbered = ids.SequenceEqual(Enumerable.Range(0, rows.Count).Select(i => "CALIBRATION_TIMING_APPLY-" + i));
        bool buttons = rows.All(r => r.Q<Button>(r.name + "-input") != null);
        string none = QuartzHost.Translate("CALIBRATION_TIMING_NONE");
        int notes = Body.Query<Label>().Where(l => l.text == none).ToList().Count, wantNotes = (map.Count == 0 ? 1 : 0) + (all.Count == 0 ? 1 : 0);
        check($"{CalibrationPage}: the timing history lists each timing newest first with Apply, numbered in its Id, and a note under an empty list",
            got.SequenceEqual(want) && numbered && buttons && notes == wantNotes,
            $"got={string.Join(",", got)} want={string.Join(",", want)} numbered={numbered} buttons={buttons} notes={notes}/{wantNotes}");
    }
}
#endif

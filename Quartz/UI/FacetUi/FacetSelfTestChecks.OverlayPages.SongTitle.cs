#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Features.SongTitle;
using Quartz.Overlay;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Overlay ▸ Song Title in the new menu: its rows against the classic page, its two columns' edges, the strip switch seen by
/// the classic page, the shadow hotkey, and the preview pane, which shows the placeholder title outside a level, frames the
/// drawn text however long, and draws a shadow switched on while it shows.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string SongTitlePage = "overlay.songtitle";
    private static SongTitleSettings SongTitleConf => SongTitleOverlay.Conf;

    // Rows 1 to 13 of the classic page.
    private static readonly string[] SongTitleRows = [
        "SONGTITLE_FORMAT", "SONGTITLE_STRIP", "SONGTITLE_ANCHOR_LABEL", "FONT_OVERLAY", "SONGTITLE_FONTSIZE", "SONGTITLE_MASTER", "SONGTITLE_COLOR",
        "SONGTITLE_SHADOW", "SONGTITLE_SHADOW_X", "SONGTITLE_SHADOW_Y", "SONGTITLE_SHADOW_SOFT", "SONGTITLE_SHADOW_COLOR", "SONGTITLE_RESETPOS",
    ];

    private static IEnumerator OverlaySongTitle(Action<string, bool, string> check, Action<string> info) {
        SongTitleOverlay.EnsureConf();
        bool shadow = SongTitleConf.ShadowEnabled;
        Color colour = SongTitleConf.GetColor();
        yield return OnPage("songtitle", SongTitlePage, () => {
            SongTitleConf.ShadowEnabled = true;   // its rows show for the parity check
            SongTitleOverlay.ApplyShadow();
        }, () => {
            SongTitleConf.ShadowEnabled = shadow;
            SongTitleConf.SetColor(colour);
            SongTitleOverlay.Apply();
            SongTitleOverlay.Save();
        }, () => SongTitleChecks(check, info, colour));
    }

    private static IEnumerator SongTitleChecks(Action<string, bool, string> check, Action<string> info, Color colour) {
        yield return Parity(check, SongTitlePage, SongTitleRows);
        yield return ColumnEdges(check, SongTitlePage, SongTitleOverlay.FontKey);
        yield return ClassicSees(check, SongTitlePage, "SONGTITLE_STRIP", Bind.To(() => SongTitleConf.StripFormatting, v => {
            SongTitleConf.StripFormatting = v;
            SongTitleOverlay.Save();
        }));
        yield return Hotkey(check, SongTitlePage, "SONGTITLE_SHADOW", "songtitle_shadow", null, Bind.To(() => SongTitleConf.ShadowEnabled, v => {
            SongTitleConf.ShadowEnabled = v;
            SongTitleOverlay.ApplyShadow();
            SongTitleOverlay.Save();
        }));
        // Outside a level there is no title: the pane shows the placeholder artist and title through the format, as arranging does.
        string placeholder = SongTitleOverlay.BuildBody(true);
        int frames = 0;
        for(; frames < 10 && !SongTitleShows(placeholder); frames++) yield return null;
        check($"{SongTitlePage}: outside a level the preview shows the placeholder title through the format", SongTitleShows(placeholder),
            $"frames={frames} target={OverlayPreview.Target} text='{SongTitleText()?.text}' want='{placeholder}'");
        yield return SongTitleLongFits(check);
        yield return SongTitleShadowDrawn(check);
        // Through the row's own picker: the text colour's channels set to red (green where it is red already), read off the title.
        Color want = SameRgb(colour, Color.red) ? Color.green : Color.red;
        yield return PreviewChecks(check, info, SongTitlePage, SongTitleOverlay.FontKey, () => SetChannels("SONGTITLE_COLOR", want),
            () => SongTitleText() is { } text && SameRgb(text.color, want));
    }

    // A long title widens the pane's frame to the drawn text, so it fits the pane instead of running past its sides.
    private static IEnumerator SongTitleLongFits(Action<string, bool, string> check) {
        string was = SongTitleConf.Format;
        RectTransform frame = OverlayPreview.FrameOf(SongTitleOverlay.FontKey)?.FirstOrDefault();
        bool Fits() => frame != null && SongTitleText() is { } t && t.GetRenderedValues() is { x: > 1000f } r
            && Mathf.Abs(frame.rect.width - r.x) <= 1f && Mathf.Abs(frame.rect.height - r.y) <= 1f
            && Vector2.Distance(frame.localPosition, t.textBounds.center) <= 1f;
        try {
            SongTitleConf.Format = new string('W', 80);   // the longest the row takes; here only, never saved
            int frames = 0;
            for(; frames < 10 && !Fits(); frames++) yield return null;
            check($"{SongTitlePage}: a long title is framed as drawn: the pane's frame takes the text's rendered size and centre within 10 frames (±1)",
                Fits(), $"frames={frames} frame={(frame == null ? "none" : frame.rect.size + "@" + (Vector2)frame.localPosition)} "
                + $"drawn={SongTitleText()?.GetRenderedValues()}@{(Vector2)(SongTitleText()?.textBounds.center ?? default)}");
        } finally {
            SongTitleConf.Format = was;
        }
        yield return Frames(2);
    }

    // A shadow switched on through its row while the pane shows builds its own canvas under the borrowed one: within 3 frames
    // that canvas, and all else under the borrowed one, is on the preview layer, which the pane's camera draws.
    private static IEnumerator SongTitleShadowDrawn(Action<string, bool, string> check) {
        string tag = $"{SongTitlePage}: a shadow switched on with the pane shown is drawn: within 3 frames its new canvas is on the preview layer";
        int frames = 0;
        for(; frames < 10 && OverlayPreview.Current is not { Released: false }; frames++) yield return null;
        OverlayPreview.Lease lease = OverlayPreview.Current;
        VisualElement track = Body.Q(name: "SONGTITLE_SHADOW-input");
        TextMeshProUGUI text = SongTitleText();
        if(lease == null || track == null || text == null) {
            check(tag, false, $"lease={lease != null} row={track != null} text={text != null}");
            yield break;
        }
        if(SongTitleConf.ShadowEnabled) Click(track);   // off through the row, then its canvas goes: the next one is made afresh
        TMPTextShadow.Remove(text);
        yield return Frames(1);
        Click(track);
        int nested = 0, off = -1;
        bool Drawn() {
            Canvas borrowed = lease.Canvas;
            nested = borrowed.GetComponentsInChildren<Canvas>(true).Count(c => c != borrowed);
            off = borrowed.GetComponentsInChildren<Transform>(true).Count(t => t.gameObject.layer != lease.Layer);
            return nested > 0 && off == 0;
        }
        for(frames = 0; frames < 3 && !Drawn(); frames++) yield return null;
        check(tag, SongTitleConf.ShadowEnabled && !lease.Released && Drawn(),
            $"on={SongTitleConf.ShadowEnabled} frames={frames} nested={nested} offLayer={off} layer={lease.Layer} relayers={OverlayPreview.Relayers}");
    }

    private static TextMeshProUGUI SongTitleText() => OverlayPreview.ShownOf(SongTitleOverlay.FontKey)?.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();

    private static bool SongTitleShows(string placeholder) =>
        OverlayPreview.Is(SongTitleOverlay.FontKey) && SongTitleText() is { } text && text.isActiveAndEnabled && placeholder.Length > 0 && text.text == placeholder;
}
#endif

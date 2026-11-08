#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using Quartz.Features.Interop;
using Quartz.Features.Judgement;
using Quartz.Overlay;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// Overlay ▸ Judgement in the new menu: its rows against the classic page, its two columns' edges, the shadow switch seen by
/// the classic page, and the preview pane, which shows sample counts outside a level.
/// </summary>
internal static partial class FacetSelfTestChecks {
    private const string JudgementPage = "overlay.judgement";
    private static JudgementSettings JudgementConf => JudgementOverlay.Conf;

    // Rows 1 to 13 of the classic page; the XPerfect row only where that mod is installed.
    private static string[] JudgementRows() => [.. new[] {
        "JUDGEMENT_XPERFECT", "JUDGEMENT_ANCHOR_LABEL", "FONT_OVERLAY", "JUDGEMENT_SIZE", "JUDGEMENT_SPACING", "JUDGEMENT_SHADOW_ENABLED",
        "JUDGEMENT_SHADOW_X", "JUDGEMENT_SHADOW_Y", "JUDGEMENT_SHADOW_SOFTNESS", "JUDGEMENT_SHADOW_COLOR", "JUDGEMENT_RESETPOS",
        "SECTION_JUDGEMENT_POPUP_FONT", "FONT_JUDGEMENT_POPUP_SIZE",
    }.Where(k => k != "JUDGEMENT_XPERFECT" || XPerfectBridge.Installed)];

    private static IEnumerator OverlayJudgement(Action<string, bool, string> check, Action<string> info) {
        JudgementOverlay.EnsureConf();
        bool shadow = JudgementConf.TextShadowEnabled, popup = MainCore.Conf.FontJudgement;
        float size = JudgementConf.Size;
        yield return OnPage("judgement", JudgementPage, () => {
            JudgementConf.TextShadowEnabled = true;   // its rows show for the parity check
            JudgementOverlay.Apply();
            MainCore.Conf.FontJudgement = true;   // so does the popup font's size row; for this run only, never saved
        }, () => {
            MainCore.Conf.FontJudgement = popup;
            JudgementConf.TextShadowEnabled = shadow;
            JudgementConf.Size = size;
            JudgementOverlay.Apply();
            JudgementOverlay.Save();
        }, () => JudgementChecks(check, info, size));
    }

    private static IEnumerator JudgementChecks(Action<string, bool, string> check, Action<string> info, float size) {
        yield return Parity(check, JudgementPage, JudgementRows());
        yield return ColumnEdges(check, JudgementPage, JudgementOverlay.FontKey);
        yield return ClassicSees(check, JudgementPage, "JUDGEMENT_SHADOW_ENABLED", Bind.To(() => JudgementConf.TextShadowEnabled, v => {
            JudgementConf.TextShadowEnabled = v;
            JudgementOverlay.Apply();
            JudgementOverlay.Save();
        }));
        // Outside a level every count is 0: the pane shows the sample row, whose perfect slot (412) is drawn, X judgements or not.
        int frames = 0;
        for(; frames < 10 && !JudgementSampleShown(); frames++) yield return null;
        check($"{JudgementPage}: outside a level the preview shows the sample counts, the perfect slot's 412 included", JudgementSampleShown(),
            $"frames={frames} target={OverlayPreview.Target} xperfect={XPerfectBridge.Active} text='{JudgementLabel()?.GetParsedText()}'");
        // Through the row's own slider: size 1.5 (1.2 where it is 1.5 already), read off the counts' font size.
        float want = Mathf.Abs(size - 1.5f) > 0.005f ? 1.5f : 1.2f;
        yield return PreviewChecks(check, info, JudgementPage, JudgementOverlay.FontKey,
            () => {
                if(QuartzShell.Surface?.Root.Q(name: "JUDGEMENT_SIZE-input") is not Slider s) return false;
                s.value = (want - 0.3f) / (3f - 0.3f);   // the track runs 0 to 1 over the row's range
                return true;
            },
            () => JudgementLabel() is { } label && Mathf.Abs(label.fontSize - 38f * want) < 0.5f);
    }

    // The compact row's one label, else the first slot's.
    private static TextMeshProUGUI JudgementLabel() {
        Transform row = OverlayPreview.ShownOf(JudgementOverlay.FontKey)?.transform;
        return (row?.Find("RowLabel") ?? row?.Find("Judgement_0"))?.GetComponent<TextMeshProUGUI>();
    }

    private static bool JudgementSampleShown() =>
        OverlayPreview.Is(JudgementOverlay.FontKey) && OverlayPreview.ShownOf(JudgementOverlay.FontKey) is { activeInHierarchy: true } row
        && row.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.GetParsedText().Contains("412"));
}
#endif

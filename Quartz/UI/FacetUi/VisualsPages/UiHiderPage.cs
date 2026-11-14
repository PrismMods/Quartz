#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Features.UiHider;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.VisualsPages;
/// <summary>Visuals ▸ UI Hiding in the new menu, row for row as PageUiHider: the mode and its shortcut, then the two
/// profiles folded shut.</summary>
internal static class UiHiderPage {
    private static UiHiderSettings C => UiHider.Conf;
    private static readonly UiHiderSettings Def = new();

    // Each profile's flags in the classic page's order: the Lang key's tail and the field.
    private static readonly (string X, Func<UiHiderProfile, bool> Get, Action<UiHiderProfile, bool> Set)[] Flags = [
        ("ALL", f => f.HideEverything, (f, v) => f.HideEverything = v),
        ("JUDG", f => f.HideJudgment, (f, v) => f.HideJudgment = v),
        ("MISS", f => f.HideMissIndicators, (f, v) => f.HideMissIndicators = v),
        ("TITLE", f => f.HideTitle, (f, v) => f.HideTitle = v),
        ("OTTO", f => f.HideOtto, (f, v) => f.HideOtto = v),
        ("DIFF", f => f.HideTimingTarget, (f, v) => f.HideTimingTarget = v),
        ("NOFAIL", f => f.HideNoFailIcon, (f, v) => f.HideNoFailIcon = v),
        ("BETA", f => f.HideBeta, (f, v) => f.HideBeta = v),
        ("RESULT", f => f.HideResult, (f, v) => f.HideResult = v),
        ("METER", f => f.HideHitErrorMeter, (f, v) => f.HideHitErrorMeter = v),
        ("FLASH", f => f.HideLastFloorFlash, (f, v) => f.HideLastFloorFlash = v),
        ("SHORTCUT", f => f.HideShortcutHints, (f, v) => f.HideShortcutHints = v),
    ];

    internal static void Build(VisualElement p) {
        UiHider.EnsureConf();
        ShellPage.Feature(p, "uihider", "SECTION_UI_HIDING", "UIHIDER_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        Action save = UiHider.Save;
        VisualElement card = Controls.Card(p, null);
        ShellRows.Toggle(card, "UIH_RECMODE", "uih_recmode", Bind.To(() => C.RecordingMode, v => C.RecordingMode = v), UiHider.ApplyNow, save,
            o => { o.Default = Def.RecordingMode; o.Tooltip = "DESC_UIH_RECMODE"; });
        ShellRows.Toggle(card, "UIH_USESHORTCUT", "uih_useshortcut", Bind.To(() => C.UseShortcut, v => C.UseShortcut = v), null, save,
            o => o.Default = Def.UseShortcut);
        ShellRows.Chord(card, "UIH_SHORTCUT", () => ((Keybind.KeyModifier)C.ShortcutModifier, (KeyCode)C.ShortcutKey), (m, k) => {
            C.ShortcutModifier = (int)m;
            C.ShortcutKey = (int)k;
        }, save, o => o.Default = Keybind.Format((Keybind.KeyModifier)Def.ShortcutModifier, (KeyCode)Def.ShortcutKey));
        Profile(card, "SECTION_PLAYING_PROFILE", "PLAY", () => C.Playing, Def.Playing);
        Profile(card, "SECTION_RECORDING_PROFILE", "REC", () => C.Recording, Def.Recording);
    }

    // A profile's twelve switches in a shut section; the shortcut-hints one also redraws the hints and says so in its tooltip.
    private static void Profile(VisualElement card, string titleKey, string tag, Func<UiHiderProfile> profile, UiHiderProfile def) {
        VisualElement body = Controls.Section(card, titleKey, o => {
            o.Id = titleKey;
            o.Open = false;
        });
        foreach(var (x, get, set) in Flags) {
            string key = $"UIH_{tag}_{x}";
            bool hints = x == "SHORTCUT";
            ShellRows.Toggle(body, key, key.ToLowerInvariant(), Bind.To(() => get(profile()), v => set(profile(), v)),
                hints ? () => { UiHider.ApplyNow(); UiHider.RefreshShortcutHints(); } : UiHider.ApplyNow, UiHider.Save, o => {
                    o.Default = get(def);
                    if(hints) o.Tooltip = $"DESC_UIH_{tag}_SHORTCUT";
                });
        }
    }
}
#endif

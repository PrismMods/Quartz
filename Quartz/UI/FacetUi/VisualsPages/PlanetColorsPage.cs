#if !IL2CPP
using Facet;
using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Features.PlanetColors;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.VisualsPages;
/// <summary>
/// Visuals ▸ Planet Colors in the new menu, row for row as PagePlanetColors: the switches, each planet's colours, the ring,
/// then each planet's overlay image. Colours and opacities apply on every write; opacities and sizes save on release.
/// </summary>
internal static class PlanetColorsPage {
    private static PlanetColorsSettings C => PlanetColors.Conf;
    private static readonly PlanetColorsSettings Def = new();

    internal static void Build(VisualElement p) {
        PlanetColors.EnsureConf();
        ShellPage.Feature(p, "planetcolors", "SECTION_PLANET_COLORS", "PLANETCOLORS_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        Action apply = PlanetColors.Refresh, save = PlanetColors.Save;
        VisualElement top = Controls.Card(p, null);
        ShellRows.Toggle(top, "PCOL_RECOLOR", "pcol_recolor", Bind.To(() => C.RecolorPlanets, v => C.RecolorPlanets = v), () => {
            PlanetColors.Restore();
            PlanetColors.Refresh();
        }, save, o => { o.Default = Def.RecolorPlanets; o.Tooltip = "DESC_PCOL_RECOLOR"; });
        ShellRows.Toggle(top, "PCOL_SEP_TAIL", "pcol_sep_tail", Bind.To(() => C.SeparateTailColor, v => C.SeparateTailColor = v), apply, save,
            o => { o.Default = Def.SeparateTailColor; o.Tooltip = "DESC_PCOL_SEP_TAIL"; });

        // No Lang key names the planets as a group: the card is untitled, each planet under a header of its own.
        VisualElement planets = Controls.Card(p, null);
        Func<bool> tails = () => C.SeparateTailColor;
        for(int i = 0; i < PlanetColorsSettings.Slots; i++) {
            int s = i;
            string n = (s + 1).ToString();
            Controls.Header(planets, "HEADING_PLANET_" + n).name = "HEADING_PLANET_" + n;
            ShellRows.Colour(planets, "PCOL_BALL" + n, () => new Color(C.BallR[s], C.BallG[s], C.BallB[s]), c => C.SetBallRgb(s, c), apply, save, false,
                o => o.Default = new Color(Def.BallR[s], Def.BallG[s], Def.BallB[s]));
            Opacity(planets, "PCOL_BALLOP" + n, () => C.BallOpacity[s], v => C.BallOpacity[s] = v, Def.BallOpacity[s]);
            ShellRows.Colour(planets, "PCOL_TAIL" + n, () => new Color(C.TailR[s], C.TailG[s], C.TailB[s]), c => C.SetTailRgb(s, c), apply, save, false, o => {
                o.Default = new Color(Def.TailR[s], Def.TailG[s], Def.TailB[s]);
                o.VisibleWhen = tails;
            });
            Opacity(planets, "PCOL_TAILOP" + n, () => C.TailOpacity[s], v => C.TailOpacity[s] = v, Def.TailOpacity[s]);
        }

        VisualElement ring = Controls.Card(p, "HEADING_RING", o => o.Id = "HEADING_RING");
        ShellRows.Toggle(ring, "PCOL_RINGON", "pcol_ringon", Bind.To(() => C.EnableRingRecolor, v => C.EnableRingRecolor = v), apply, save,
            o => { o.Default = Def.EnableRingRecolor; o.Tooltip = "DESC_PCOL_RING"; });
        ShellRows.Toggle(ring, "PCOL_SEP_RING", "pcol_sep_ring", Bind.To(() => C.SeparateRingColor, v => C.SeparateRingColor = v), apply, save,
            o => { o.Default = Def.SeparateRingColor; o.Tooltip = "DESC_PCOL_SEP_RING"; });
        Func<bool> shared = () => !C.SeparateRingColor, own = () => C.SeparateRingColor;
        ShellRows.Colour(ring, "PCOL_RINGCOL", () => new Color(C.RingR, C.RingG, C.RingB), C.SetRingRgb, apply, save, false, o => {
            o.Default = new Color(Def.RingR, Def.RingG, Def.RingB);
            o.VisibleWhen = shared;
        });
        Opacity(ring, "PCOL_RINGOP", () => C.RingA, v => C.RingA = v, Def.RingA, shared);
        for(int i = 0; i < PlanetColorsSettings.Slots; i++) {
            int s = i;
            string n = (s + 1).ToString();
            ShellRows.Colour(ring, "PCOL_RINGCOL" + n, () => new Color(C.SlotRingR[s], C.SlotRingG[s], C.SlotRingB[s]), c => C.SetSlotRingRgb(s, c), apply, save,
                false, o => {
                    o.Default = new Color(Def.SlotRingR[s], Def.SlotRingG[s], Def.SlotRingB[s]);
                    o.VisibleWhen = own;
                });
            Opacity(ring, "PCOL_RINGOP" + n, () => C.SlotRingA[s], v => C.SlotRingA[s] = v, Def.SlotRingA[s], own);
        }

        VisualElement overlay = Controls.Card(p, "HEADING_PLANET_OVERLAY", o => o.Id = "HEADING_PLANET_OVERLAY");
        ShellRows.Toggle(overlay, "PCOL_OVERLAY_ON", "pcol_overlay_on", Bind.To(() => C.EnableOverlay, v => C.EnableOverlay = v), PlanetColors.ReloadOverlayImages,
            save, o => { o.Default = Def.EnableOverlay; o.Tooltip = "DESC_PCOL_OVERLAY_ON"; });
        for(int i = 0; i < PlanetColorsSettings.Slots; i++) {
            int s = i;
            string n = (s + 1).ToString();
            // The classic index lists each planet's header twice: here is the second.
            Controls.Header(overlay, "HEADING_PLANET_" + n).name = "HEADING_PLANET_" + n + "#2";
            ShellRows.FilePick(overlay, "PCOL_OVERLAY_PICK" + n, "PCOL_OVERLAY_CLEAR" + n, () => {
                string path = C.GetOverlayPath(s);
                return string.IsNullOrWhiteSpace(path) ? MainCore.Tr.Get("PCOL_OVERLAY_NONE", "No image set") : Path.GetFileName(path);
            }, () => {
                string picked = PickOverlayImage();
                if(!string.IsNullOrEmpty(picked)) SetOverlay(s, picked);
            }, () => SetOverlay(s, ""));
            ShellRows.ReleaseSlider(overlay, "PCOL_OVERLAY_SCALE" + n, Bind.To(() => C.OverlayScale[s], v => C.OverlayScale[s] = v),
                PlanetColorsSettings.MinOverlayScale, PlanetColorsSettings.MaxOverlayScale, 0f, "0.000'x'", apply, save, o => o.Default = Def.OverlayScale[s]);
        }
    }

    // 0-100 over a 0-1 field, applied on every write and saved on release, as the classic opacity sliders.
    private static void Opacity(VisualElement card, string key, Func<float> get, Action<float> set, float def, Func<bool> visible = null) =>
        ShellRows.ReleaseSlider(card, key, Bind.To(() => get() * 100f, v => set(v / 100f)), 0f, 100f, 1f, "0' %'", PlanetColors.Refresh, PlanetColors.Save,
            o => {
                o.Default = def * 100f;
                o.VisibleWhen = visible;
            });

    private static void SetOverlay(int slot, string path) {
        UICore.ClassicStale = true;   // the classic page reads the file name at build
        C.SetOverlayPath(slot, path);
        PlanetColors.ReloadOverlayImages();
        PlanetColors.Save();
    }

    // The classic page's own picker, which is private to it.
    private static string PickOverlayImage() {
        try {
            return FileDialog.PickFile("", "Image", ["png", "jpg", "jpeg"], "Select planet overlay image");
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(PlanetColorsPage)}] overlay PickFile failed: {e}");
            return null;
        }
    }
}
#endif

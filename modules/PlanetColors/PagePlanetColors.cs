using Quartz.Compat.Game;
using Quartz.Core;
using Quartz.Features.PlanetColors;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using TMPro;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PagePlanetColors {
    public static void Create(RectTransform parent) =>
        CreatePlanetColors(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    private static void CreatePlanetColors(Transform content) {
        PlanetColors.EnsureConf();
        PlanetColorsSettings conf = PlanetColors.Conf;
        PlanetColorsSettings def = new();
        void Apply() => PlanetColors.Refresh();
        void Save() => PlanetColors.Save();
        var sec = GenerateUI.FlatSection(
            content, "Planet Colors",
            v => {
                conf.Enabled = v;
                if(v) PlanetColors.Refresh();
                else PlanetColors.Restore();
                Save();
            },
            conf.Enabled,
            "Enable Planet Colors", "planetcolors_enable", def.Enabled
        );
        RectTransform[] tailColorRows = new RectTransform[PlanetColorsSettings.Slots];
        void RefreshTailRows() {
            foreach(RectTransform row in tailColorRows) row?.gameObject.SetActive(conf.SeparateTailColor);
        }
        GenerateUI.ToggleTip(
            sec.Body,
            def.SeparateTailColor,
            conf.SeparateTailColor,
            v => {
                conf.SeparateTailColor = v;
                RefreshTailRows();
                Apply();
                Save();
            },
            "Separate Tail Color",
            "pcol_sep_tail",
            "Off: tails use the ball color (with their own opacity). On: each planet's tail gets its own color."
        );
        for(int i = 0; i < PlanetColorsSettings.Slots; i++) {
            int slot = i;
            string n = (slot + 1).ToString();
            GenerateUI.Localize(
                GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)),
                "HEADING_PLANET_" + n,
                $"Planet {n}"
            );
            GenerateUI.ColorPicker(
                GenerateUI.Row(sec.Body),
                new Color(def.BallR[slot], def.BallG[slot], def.BallB[slot]),
                new Color(conf.BallR[slot], conf.BallG[slot], conf.BallB[slot]),
                c => { conf.SetBallRgb(slot, c); Apply(); },
                c => { conf.SetBallRgb(slot, c); Apply(); Save(); },
                $"Planet {n} Color",
                $"pcol_ball{n}",
                showAlpha: false
            );
            UISlider ballOp = GenerateUI.Slider(
                GenerateUI.Row(sec.Body),
                def.BallOpacity[slot] * 100f, 0f, 100f, conf.BallOpacity[slot] * 100f,
                Mathf.Round, null, null,
                $"Planet {n} Ball Opacity",
                $"pcol_ballop{n}"
            );
            ballOp.Format = "0' %'";
            ballOp.OnChanged = v => { conf.BallOpacity[slot] = v / 100f; Apply(); };
            ballOp.OnComplete = v => { conf.BallOpacity[slot] = v / 100f; Apply(); Save(); };
            tailColorRows[slot] = GenerateUI.Row(sec.Body);
            GenerateUI.ColorPicker(
                tailColorRows[slot],
                new Color(def.TailR[slot], def.TailG[slot], def.TailB[slot]),
                new Color(conf.TailR[slot], conf.TailG[slot], conf.TailB[slot]),
                c => { conf.SetTailRgb(slot, c); Apply(); },
                c => { conf.SetTailRgb(slot, c); Apply(); Save(); },
                $"Planet {n} Tail Color",
                $"pcol_tail{n}",
                showAlpha: false
            );
            UISlider tailOp = GenerateUI.Slider(
                GenerateUI.Row(sec.Body),
                def.TailOpacity[slot] * 100f, 0f, 100f, conf.TailOpacity[slot] * 100f,
                Mathf.Round, null, null,
                $"Planet {n} Tail Opacity",
                $"pcol_tailop{n}"
            );
            tailOp.Format = "0' %'";
            tailOp.OnChanged = v => { conf.TailOpacity[slot] = v / 100f; Apply(); };
            tailOp.OnComplete = v => { conf.TailOpacity[slot] = v / 100f; Apply(); Save(); };
        }
        GenerateUI.Localize(
            GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)),
            "HEADING_RING",
            "Ring"
        );
        GenerateUI.Toggle(
            GenerateUI.Row(sec.Body),
            def.EnableRingRecolor,
            conf.EnableRingRecolor,
            v => { conf.EnableRingRecolor = v; Apply(); Save(); },
            "Recolor Ring",
            "pcol_ringon"
        ).Rect.AddToolTip(
            "DESC_PCOL_RING",
            "Paint the planet ring a custom colour. When off, the ring is hidden while planet colours are active."
        );
        RectTransform[] sharedRingRows = new RectTransform[2];
        RectTransform[] slotRingRows = new RectTransform[PlanetColorsSettings.Slots * 2];
        void RefreshRingRows() {
            foreach(RectTransform row in sharedRingRows) row?.gameObject.SetActive(!conf.SeparateRingColor);
            foreach(RectTransform row in slotRingRows) row?.gameObject.SetActive(conf.SeparateRingColor);
        }
        GenerateUI.ToggleTip(
            sec.Body,
            def.SeparateRingColor,
            conf.SeparateRingColor,
            v => {
                conf.SeparateRingColor = v;
                RefreshRingRows();
                Apply();
                Save();
            },
            "Separate Ring Color",
            "pcol_sep_ring",
            "Off: every planet's ring uses one colour. On: each planet gets its own ring colour."
        );
        sharedRingRows[0] = GenerateUI.Row(sec.Body);
        GenerateUI.ColorPicker(
            sharedRingRows[0],
            new Color(def.RingR, def.RingG, def.RingB),
            new Color(conf.RingR, conf.RingG, conf.RingB),
            c => { conf.SetRingRgb(c); Apply(); },
            c => { conf.SetRingRgb(c); Apply(); Save(); },
            "Ring Color",
            "pcol_ringcol",
            showAlpha: false
        );
        sharedRingRows[1] = GenerateUI.Row(sec.Body);
        UISlider ringOp = GenerateUI.Slider(
            sharedRingRows[1],
            def.RingA * 100f, 0f, 100f, conf.RingA * 100f,
            Mathf.Round, null, null,
            "Ring Opacity",
            "pcol_ringop"
        );
        ringOp.Format = "0' %'";
        ringOp.OnChanged = v => { conf.RingA = v / 100f; Apply(); };
        ringOp.OnComplete = v => { conf.RingA = v / 100f; Apply(); Save(); };
        for(int i = 0; i < PlanetColorsSettings.Slots; i++) {
            int slot = i;
            string n = (slot + 1).ToString();
            slotRingRows[slot * 2] = GenerateUI.Row(sec.Body);
            GenerateUI.ColorPicker(
                slotRingRows[slot * 2],
                new Color(def.SlotRingR[slot], def.SlotRingG[slot], def.SlotRingB[slot]),
                new Color(conf.SlotRingR[slot], conf.SlotRingG[slot], conf.SlotRingB[slot]),
                c => { conf.SetSlotRingRgb(slot, c); Apply(); },
                c => { conf.SetSlotRingRgb(slot, c); Apply(); Save(); },
                $"Planet {n} Ring Color",
                $"pcol_ringcol{n}",
                showAlpha: false
            );
            slotRingRows[(slot * 2) + 1] = GenerateUI.Row(sec.Body);
            UISlider slotRingOp = GenerateUI.Slider(
                slotRingRows[(slot * 2) + 1],
                def.SlotRingA[slot] * 100f, 0f, 100f, conf.SlotRingA[slot] * 100f,
                Mathf.Round, null, null,
                $"Planet {n} Ring Opacity",
                $"pcol_ringop{n}"
            );
            slotRingOp.Format = "0' %'";
            slotRingOp.OnChanged = v => { conf.SlotRingA[slot] = v / 100f; Apply(); };
            slotRingOp.OnComplete = v => { conf.SlotRingA[slot] = v / 100f; Apply(); Save(); };
        }
        CreatePlanetOverlay(sec.Body, conf, def);
        RefreshTailRows();
        RefreshRingRows();
    }
    private static void CreatePlanetOverlay(
        Transform body,
        PlanetColorsSettings conf,
        PlanetColorsSettings def
    ) {
        void Apply() => PlanetColors.Refresh();
        void Save() => PlanetColors.Save();
        GenerateUI.Localize(
            GenerateUI.AddTextH1(GenerateUI.Row(body)),
            "HEADING_PLANET_OVERLAY",
            "Overlay Image"
        );
        GenerateUI.ToggleTip(
            body,
            def.EnableOverlay,
            conf.EnableOverlay,
            v => {
                conf.EnableOverlay = v;
                PlanetColors.ReloadOverlayImages();
                Save();
            },
            "Enable Overlay Image",
            "pcol_overlay_on",
            "Draws a picture on top of each planet. Relative paths are read from the level's own folder, so charts can ship their own art. Hidden while you are editing a level."
        );
        for(int i = 0; i < PlanetColorsSettings.Slots; i++) {
            int slot = i;
            string n = (slot + 1).ToString();
            GenerateUI.Localize(
                GenerateUI.AddTextH1(GenerateUI.Row(body)),
                "HEADING_PLANET_" + n,
                $"Planet {n}"
            );
            TextMeshProUGUI status = GenerateUI.AddMutedText(GenerateUI.Row(body, 30f), 17f, 0.45f);
            void RefreshStatus() {
                string path = conf.GetOverlayPath(slot);
                status.text = string.IsNullOrWhiteSpace(path)
                    ? MainCore.Tr.Get("PCOL_OVERLAY_NONE", "No image set")
                    : Path.GetFileName(path);
            }
            GenerateUI.Button(
                GenerateUI.Row(body),
                () => {
                    string picked = PickOverlayImage();
                    if(string.IsNullOrEmpty(picked)) return;
                    conf.SetOverlayPath(slot, picked);
                    RefreshStatus();
                    PlanetColors.ReloadOverlayImages();
                    Save();
                },
                $"Planet {n} Overlay Image",
                $"pcol_overlay_pick{n}"
            );
            GenerateUI.Button(
                GenerateUI.Row(body),
                () => {
                    conf.SetOverlayPath(slot, "");
                    RefreshStatus();
                    PlanetColors.ReloadOverlayImages();
                    Save();
                },
                $"Clear Planet {n} Overlay",
                $"pcol_overlay_clear{n}"
            ).SetSecondary();
            UISlider scale = GenerateUI.Slider(
                GenerateUI.Row(body),
                def.OverlayScale[slot],
                PlanetColorsSettings.MinOverlayScale,
                PlanetColorsSettings.MaxOverlayScale,
                conf.OverlayScale[slot],
                null, null, null,
                $"Planet {n} Overlay Size",
                $"pcol_overlay_scale{n}"
            );
            scale.Format = "0.000'x'";
            scale.OnChanged = v => { conf.OverlayScale[slot] = v; Apply(); };
            scale.OnComplete = v => { conf.OverlayScale[slot] = v; Apply(); Save(); };
            RefreshStatus();
        }
    }
    private static string PickOverlayImage() {
        try {
            return FileDialog.PickFile(
                "", "Image", ["png", "jpg", "jpeg"], "Select planet overlay image"
            );
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(PagePlanetColors)}] overlay PickFile failed: {e}");
            return null;
        }
    }
}

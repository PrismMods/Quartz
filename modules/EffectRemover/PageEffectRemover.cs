using Quartz.Core;
using Quartz.Features.EffectRemover;
using Quartz.Resource;
using Quartz.UI.Generator;
using Quartz.UI.Objects.Impl;
using Quartz.UI.Utility;
using UnityEngine;
namespace Quartz.UI.Factory.Page;
public static class PageEffectRemover {
    public static void Create(RectTransform parent) =>
        CreateEffectRemover(Quartz.UI.Factory.PageFactory.CreateScrollablePage(parent));
    private static void CreateEffectRemover(RectTransform content) {
        EffectRemover.EnsureConf();
        EffectRemoverSettings conf = EffectRemover.Conf;
        EffectRemoverSettings def = new();
        void Save() => EffectRemover.Save();
        var sec = GenerateUI.FlatSection(
            content.transform, "Effect Remover",
            v => {
                conf.On = v;
                EffectRemover.RefreshEditorSaveButtons();
                Save();
            },
            conf.On,
            "Enable Effect Remover", "effectremover_enable", def.On
        );
        GenerateUI.DropDown(
            GenerateUI.Row(sec.Body),
            EffectRemoverSettings.ModeEnhanced,
            conf.Mode,
            new[] { EffectRemoverSettings.ModeSimple, EffectRemoverSettings.ModeEnhanced },
            m => MainCore.Tr.Get(
                m == EffectRemoverSettings.ModeSimple ? "FXRM_MODE_SIMPLE" : "FXRM_MODE_ENHANCED",
                m == EffectRemoverSettings.ModeSimple ? "Simple" : "Enhanced"),
            v => {
                conf.Mode = v;
                Save();
                EffectRemover.RefreshEditorSaveButtons();
                UICore.Rebuild();
            },
            "fxrm_mode",
            260f,
            "Mode"
        );
        if(conf.IsSimple) {
            CreateSimpleEffectRemover(sec.Body, conf, def);
        } else {
        GenerateUI.ToggleTip(
            sec.Body,
            def.EnableSave,
            conf.EnableSave,
            v => {
                conf.EnableSave = v;
                EffectRemover.RefreshEditorSaveButtons();
                Save();
            },
            "Allow Saving in Editor",
            "fxrm_enable_save",
            "The editor holds the stripped chart, so saving overwrites the file and the removed effects are gone for good. Off blocks the editor's save button while Enhanced is on."
        );
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_NON_DLC_EVENTS", "Non-DLC Events");
        RectTransform removeAllRow = null;
        RectTransform setZoomRow = null;
        RectTransform zoomSliderRow = null;
        RectTransform resetAnimRow = null;
        RectTransform resetColorRow = null;
        RectTransform tutorialPatternsRow = null;
        GenerateUI.CollapsibleSection decoTypesSection = null;
        void RefreshConditionalRows() {
            removeAllRow?.gameObject.SetActive(conf.Decorations);
            decoTypesSection?.Section.gameObject.SetActive(conf.Decorations);
            setZoomRow?.gameObject.SetActive(conf.Cameras);
            zoomSliderRow?.gameObject.SetActive(conf.Cameras && conf.SetCameraZoom);
            resetAnimRow?.gameObject.SetActive(conf.TrackAnimations);
            resetColorRow?.gameObject.SetActive(conf.TrackColors);
            tutorialPatternsRow?.gameObject.SetActive(conf.Backgrounds);
        }
        void SimpleToggle(Transform body, bool defVal, bool val, System.Action<bool> set, string label, string id) {
            GenerateUI.Toggle(
                GenerateUI.Row(body),
                defVal,
                val,
                v => {
                    set(v);
                    RefreshConditionalRows();
                    Save();
                },
                label,
                id
            );
        }
        SimpleToggle(sec.Body, def.Filters, conf.Filters, v => conf.Filters = v, "Filter", "fxrm_filters");
        SimpleToggle(sec.Body, def.AdvancedFilters, conf.AdvancedFilters, v => conf.AdvancedFilters = v, "Advanced Filter", "fxrm_advfilters");
        SimpleToggle(sec.Body, def.Decorations, conf.Decorations, v => conf.Decorations = v, "Decoration", "fxrm_decorations");
        SimpleToggle(sec.Body, def.Backgrounds, conf.Backgrounds, v => conf.Backgrounds = v, "Background", "fxrm_backgrounds");
        SimpleToggle(sec.Body, def.Cameras, conf.Cameras, v => conf.Cameras = v, "Camera", "fxrm_cameras");
        SimpleToggle(sec.Body, def.RepeatEvents, conf.RepeatEvents, v => conf.RepeatEvents = v, "Repeat Event", "fxrm_repeat");
        SimpleToggle(sec.Body, def.FrameRate, conf.FrameRate, v => conf.FrameRate = v, "Frame Rate", "fxrm_framerate");
        SimpleToggle(sec.Body, def.HitSounds, conf.HitSounds, v => conf.HitSounds = v, "HitSound", "fxrm_hitsounds");
        {
            var planet = GenerateUI.Collapsible(sec.Body, "Planet Events", startExpanded: false);
            UIToggle orbit = null, scale = null, radius = null;
            GenerateUI.Button(
                GenerateUI.Row(planet.Body),
                () => {
                    if(orbit == null || scale == null || radius == null) return;
                    bool value = !conf.PlanetOrbit && !conf.PlanetScale && !conf.PlanetRadius;
                    orbit.Set(value);
                    scale.Set(value);
                    radius.Set(value);
                },
                "Toggle All",
                "fxrm_planet_all"
            ).SetSecondary();
            orbit = GenerateUI.Toggle(
                GenerateUI.Row(planet.Body), def.PlanetOrbit, conf.PlanetOrbit,
                v => { conf.PlanetOrbit = v; Save(); }, "Planet Orbit", "fxrm_planet_orbit");
            scale = GenerateUI.Toggle(
                GenerateUI.Row(planet.Body), def.PlanetScale, conf.PlanetScale,
                v => { conf.PlanetScale = v; Save(); }, "Planet Scale", "fxrm_planet_scale");
            radius = GenerateUI.Toggle(
                GenerateUI.Row(planet.Body), def.PlanetRadius, conf.PlanetRadius,
                v => { conf.PlanetRadius = v; Save(); }, "Planet Radius", "fxrm_planet_radius");
        }
        {
            var track = GenerateUI.Collapsible(sec.Body, "Track Events", startExpanded: false);
            UIToggle anims = null, moves = null, positions = null, colors = null;
            GenerateUI.Button(
                GenerateUI.Row(track.Body),
                () => {
                    if(anims == null || moves == null || positions == null || colors == null) return;
                    bool value = !conf.TrackAnimations && !conf.TrackPositions
                        && !conf.TrackMoves && !conf.TrackColors;
                    anims.Set(value);
                    moves.Set(value);
                    positions.Set(value);
                    colors.Set(value);
                },
                "Toggle All",
                "fxrm_track_all"
            ).SetSecondary();
            anims = GenerateUI.Toggle(
                GenerateUI.Row(track.Body), def.TrackAnimations, conf.TrackAnimations,
                v => { conf.TrackAnimations = v; RefreshConditionalRows(); Save(); }, "Animate Track", "fxrm_track_anims");
            moves = GenerateUI.Toggle(
                GenerateUI.Row(track.Body), def.TrackMoves, conf.TrackMoves,
                v => { conf.TrackMoves = v; Save(); }, "Move Track", "fxrm_track_moves");
            positions = GenerateUI.Toggle(
                GenerateUI.Row(track.Body), def.TrackPositions, conf.TrackPositions,
                v => { conf.TrackPositions = v; Save(); }, "Position Track", "fxrm_track_positions");
            colors = GenerateUI.Toggle(
                GenerateUI.Row(track.Body), def.TrackColors, conf.TrackColors,
                v => { conf.TrackColors = v; RefreshConditionalRows(); Save(); }, "Track Color", "fxrm_track_colors");
        }
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_DLC_EVENTS", "DLC Events");
        SimpleToggle(sec.Body, def.HoldSounds, conf.HoldSounds, v => conf.HoldSounds = v, "HoldSound", "fxrm_holdsounds");
        SimpleToggle(sec.Body, def.HideIcons, conf.HideIcons, v => conf.HideIcons = v, "HideIcon & Judgements", "fxrm_hideicons");
        GenerateUI.Localize(GenerateUI.AddTextH1(GenerateUI.Row(sec.Body)), "HEADING_MISC", "Misc");
        {
            var decoTypes = GenerateUI.Collapsible(sec.Body, "Decoration Types", startExpanded: false);
            decoTypesSection = decoTypes;
            UIToggle planetT = null, tileT = null, imageT = null, textT = null, particleT = null, hazardT = null;
            GenerateUI.Button(
                GenerateUI.Row(decoTypes.Body),
                () => {
                    if(planetT == null || tileT == null || imageT == null || textT == null || particleT == null || hazardT == null) return;
                    bool value = !conf.DecoPlanet && !conf.DecoTiles && !conf.DecoImage
                        && !conf.DecoText && !conf.Particles && !conf.DecoFailHitbox;
                    planetT.Set(value);
                    tileT.Set(value);
                    imageT.Set(value);
                    textT.Set(value);
                    particleT.Set(value);
                    hazardT.Set(value);
                },
                "Toggle All",
                "fxrm_deco_types_all"
            ).SetSecondary();
            planetT = GenerateUI.Toggle(
                GenerateUI.Row(decoTypes.Body), def.DecoPlanet, conf.DecoPlanet,
                v => { conf.DecoPlanet = v; Save(); }, "Planet", "fxrm_deco_planet");
            tileT = GenerateUI.Toggle(
                GenerateUI.Row(decoTypes.Body), def.DecoTiles, conf.DecoTiles,
                v => { conf.DecoTiles = v; Save(); }, "Tiles", "fxrm_deco_tiles");
            imageT = GenerateUI.Toggle(
                GenerateUI.Row(decoTypes.Body), def.DecoImage, conf.DecoImage,
                v => { conf.DecoImage = v; Save(); }, "Image", "fxrm_deco_image");
            textT = GenerateUI.Toggle(
                GenerateUI.Row(decoTypes.Body), def.DecoText, conf.DecoText,
                v => { conf.DecoText = v; Save(); }, "Text", "fxrm_deco_text");
            particleT = GenerateUI.Toggle(
                GenerateUI.Row(decoTypes.Body), def.Particles, conf.Particles,
                v => { conf.Particles = v; Save(); }, "Particles", "fxrm_particles");
            hazardT = GenerateUI.Toggle(
                GenerateUI.Row(decoTypes.Body), def.DecoFailHitbox, conf.DecoFailHitbox,
                v => { conf.DecoFailHitbox = v; Save(); }, "Judgement Limit (Fail Hitbox)", "fxrm_deco_failhitbox");
            hazardT.Rect.AddToolTip(
                "DESC_FXRM_DECO_FAILHITBOX",
                "Removes decorations whose hitbox can fail your run (HitboxType: Kill), regardless of the type toggles above."
            );
        }
        removeAllRow = GenerateUI.Row(sec.Body);
        GenerateUI.Toggle(
            removeAllRow, def.RemoveAllDecorations, conf.RemoveAllDecorations,
            v => { conf.RemoveAllDecorations = v; Save(); },
            "Remove All Decorations",
            "fxrm_remove_all_deco"
        ).Rect.AddToolTip(
            "DESC_FXRM_REMOVE_ALL_DECO",
            "Off keeps decorations that judgement-conditional events reference (hit/miss feedback) and removes the rest."
        );
        SimpleToggle(sec.Body, def.LimitTrackOpacity, conf.LimitTrackOpacity,
            v => conf.LimitTrackOpacity = v,
            "Limit 'Track Opacity' Values to 100%", "fxrm_limit_opacity");
        setZoomRow = GenerateUI.Row(sec.Body);
        GenerateUI.Toggle(
            setZoomRow, def.SetCameraZoom, conf.SetCameraZoom,
            v => {
                conf.SetCameraZoom = v;
                RefreshConditionalRows();
                Save();
            },
            "Set Camera Zoom",
            "fxrm_set_zoom"
        );
        zoomSliderRow = GenerateUI.Row(sec.Body);
        UISlider zoom = GenerateUI.Slider(
            zoomSliderRow,
            def.CameraZoomScale, 100f, 1000f, conf.CameraZoomScale,
            v => Mathf.Clamp(Mathf.Round(v), 100f, 1000f), null, null,
            "Camera Zoom",
            "fxrm_zoom_scale"
        );
        zoom.Format = "0' %'";
        zoom.OnChanged = v => conf.CameraZoomScale = v;
        zoom.OnComplete = v => { conf.CameraZoomScale = v; Save(); };
        resetAnimRow = GenerateUI.Row(sec.Body);
        GenerateUI.Toggle(
            resetAnimRow, def.ResetTrackAnimation, conf.ResetTrackAnimation,
            v => { conf.ResetTrackAnimation = v; Save(); },
            "Set Track Animation to Default",
            "fxrm_reset_anim"
        );
        resetColorRow = GenerateUI.Row(sec.Body);
        GenerateUI.Toggle(
            resetColorRow, def.ResetTrackColor, conf.ResetTrackColor,
            v => { conf.ResetTrackColor = v; Save(); },
            "Set Track Color to Default",
            "fxrm_reset_color"
        );
        tutorialPatternsRow = GenerateUI.Row(sec.Body);
        GenerateUI.Toggle(
            tutorialPatternsRow, def.RemoveTutorialPatterns, conf.RemoveTutorialPatterns,
            v => { conf.RemoveTutorialPatterns = v; Save(); },
            "Turn off Tutorial Background Patterns",
            "fxrm_tutorial_patterns"
        ).Rect.AddToolTip(
            "DESC_FXRM_TUTORIAL_PATTERNS",
            "Also hides the default background's tiled pattern. Its pulsing shapes are always removed while Background is on."
        );
        RefreshConditionalRows();
        }
    }
    private static void CreateSimpleEffectRemover(
        Transform parent, EffectRemoverSettings conf, EffectRemoverSettings def) {
        void Save() => EffectRemover.Save();
        GenerateUI.CollapsibleSection excludeSection = null;
        GenerateUI.ToggleTip(
            parent, def.SimpleFilter, conf.SimpleFilter,
            v => {
                conf.SimpleFilter = v;
                excludeSection?.Section.gameObject.SetActive(v);
                Save();
            },
            "Disable Filters", "fxrm_s_filter",
            "Turns off VFX filters (Grayscale, Arcade, etc.) at runtime without changing the chart.");
        excludeSection = GenerateUI.Collapsible(parent, "Excluded Filters", startExpanded: false);
        foreach(Filter value in System.Enum.GetValues(typeof(Filter))) {
            Filter filter = value;
            GenerateUI.Toggle(
                GenerateUI.Row(excludeSection.Body),
                false,
                conf.SimpleFilterExcludeList.Contains(filter),
                v => {
                    if(v) {
                        if(!conf.SimpleFilterExcludeList.Contains(filter)) conf.SimpleFilterExcludeList.Add(filter);
                    } else {
                        conf.SimpleFilterExcludeList.Remove(filter);
                    }
                    Save();
                },
                RDString.GetEnumValue(filter),
                $"fxrm_s_filter_ex_{filter}"
            );
        }
        excludeSection.Section.gameObject.SetActive(conf.SimpleFilter);
        GenerateUI.ToggleTip(
            parent, def.SimpleAdvancedFilter, conf.SimpleAdvancedFilter,
            v => { conf.SimpleAdvancedFilter = v; Save(); },
            "Disable Advanced Filter", "fxrm_s_advfilter",
            "Turns off Advanced Filter VFX at runtime without changing the chart.");
        GenerateUI.ToggleTip(
            parent, def.SimpleBloom, conf.SimpleBloom,
            v => { conf.SimpleBloom = v; Save(); },
            "Disable Bloom", "fxrm_s_bloom", "Skips the bloom effect.");
        GenerateUI.ToggleTip(
            parent, def.SimpleFlash, conf.SimpleFlash,
            v => { conf.SimpleFlash = v; Save(); },
            "Disable Flash", "fxrm_s_flash", "Neutralises screen-flash effects.");
        GenerateUI.ToggleTip(
            parent, def.SimpleHallOfMirrors, conf.SimpleHallOfMirrors,
            v => { conf.SimpleHallOfMirrors = v; Save(); },
            "Disable Hall of Mirrors", "fxrm_s_hom", "Skips the Hall of Mirrors effect.");
        GenerateUI.ToggleTip(
            parent, def.SimpleScreenShake, conf.SimpleScreenShake,
            v => { conf.SimpleScreenShake = v; Save(); },
            "Disable Screen Shake", "fxrm_s_shake", "Skips screen-shake effects.");
        GenerateUI.ToggleTip(
            parent, def.SimpleScreenTile, conf.SimpleScreenTile,
            v => { conf.SimpleScreenTile = v; Save(); },
            "Disable Screen Tiling/Scroll", "fxrm_s_screentile",
            "Turns off screen-tiling (kaleidoscope/repeat) and screen-scroll VFX at runtime without changing the chart.");
        GenerateUI.Slider(
            GenerateUI.Row(parent),
            def.SimpleMoveTrackMax, 5f, EffectRemoverSettings.MoveTrackUpperBound + 5f,
            conf.SimpleMoveTrackMax,
            f => Mathf.Round(f / 5f) * 5f,
            _ => { },
            v => { conf.SimpleMoveTrackMax = Mathf.RoundToInt(v); Save(); },
            "Max Tile Movements", "fxrm_s_movemax"
        ).Rect.AddToolTip("DESC_FXRM_S_MOVEMAX",
            "Caps how many tiles a single Move Track event can move (around the current tile). The maximum value means unlimited.");
    }
}

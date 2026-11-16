#if !IL2CPP
using System.Text.RegularExpressions;
using Facet;
using Quartz.Core;
using Quartz.Features.EffectRemover;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The Effect Remover page in the new menu (a QuartzShell.Moved builder). Mirrors PageEffectRemover row for row.
/// Labels are Quartz Lang keys, which QuartzHost translates live. Value rows write through QuartzShell.Writes: the
/// classic page this window hands back to never reads a toggle's field again, so it rebuilds first.
/// </summary>
internal static class FacetEffectRemover {
    // Read on every call: a profile switch may replace what EffectRemover.Conf returns.
    private static EffectRemoverSettings C => EffectRemover.Conf;
    private static readonly EffectRemoverSettings Def = new();
    private static void Save() => EffectRemover.Save();
    internal static void Page(VisualElement page) {
        EffectRemover.EnsureConf();
        // The switch is the page's own: the classic menu gets its "Use this feature" row from the page factory.
        Controls.PageHeader(page, "SECTION_EFFECT_REMOVER", QuartzShell.FeatureSwitch("effectremover"), o => {
            o.Id = "FXRM_HEADER";
            o.Description = "FXRM_DESC";
        });
        // Only this part depends on the mode. Facet has no group that follows a non-bool value
        // (ToggleGroup is bool and brings its own switch), so it follows on the refresher's tick:
        // the dropdown's VisibleWhen is asked on every refresh while the window shows. A mode set
        // here, from the menu or by a profile switch then rebuilds these rows and nothing above them.
        VisualElement body = Ui.Box();
        bool? built = null;
        VisualElement decoTypes = null;
        void Follow() {
            if(body.parent == null) return;   // asked once while the dropdown is built, before body is placed
            if(C.IsSimple != built) {
                built = C.IsSimple;
                body.Clear();
                decoTypes = C.IsSimple ? Simple(body) : Enhanced(body);
            }
            // A Section has no VisibleWhen; its wrapper follows the switch instead.
            if(decoTypes != null) decoTypes.style.display = C.Decorations ? DisplayStyle.Flex : DisplayStyle.None;
        }
        Controls.Dropdown(Controls.Card(page, null), "FXRM_MODE_LABEL", Bind.To(() => C.IsSimple ? 0 : 1, v => {
            C.Mode = v == 0 ? EffectRemoverSettings.ModeSimple : EffectRemoverSettings.ModeEnhanced;
            Save();
            EffectRemover.RefreshEditorSaveButtons();
            QuartzShell.Changed();   // the search lists this mode's rows
        }), o => {
            o.Id = "FXRM_MODE";
            o.Items = ["FXRM_MODE_SIMPLE", "FXRM_MODE_ENHANCED"];
            o.Default = Def.IsSimple ? 0 : 1;
            o.VisibleWhen = () => { Follow(); return true; };
        });
        page.Add(body);
        Follow();
    }
    private static Control<bool> Flag(VisualElement p, string key, Func<EffectRemoverSettings, bool> get,
        Action<EffectRemoverSettings, bool> set, bool def, string tip = null, Func<bool> visible = null) =>
        Controls.Toggle(p, key, QuartzShell.Writes(() => get(C), v => { set(C, v); Save(); }), o => {
            o.Id = key;
            o.Default = def;
            o.Tooltip = tip;
            o.VisibleWhen = visible;
        });
    // The menu's bare "Toggle All" button: a row with no label under its section's switches. Off when any is on, otherwise all on.
    private static void ToggleAll(VisualElement p, string key, Control<bool>[] rows) =>
        Controls.Action(p, "", key, () => {
            bool value = rows.All(r => !r.Value);
            foreach(Control<bool> r in rows) r.Set(value);
        }, o => o.Id = key);
    private static VisualElement Simple(VisualElement body) {
        VisualElement p = Controls.Card(body, null);
        VisualElement excluded = Controls.ToggleGroup(p, "FXRM_S_FILTER",
            QuartzShell.Writes(() => C.SimpleFilter, v => { C.SimpleFilter = v; Save(); }),
            o => { o.Id = "FXRM_S_FILTER"; o.Default = Def.SimpleFilter; o.Tooltip = "DESC_FXRM_S_FILTER"; });
        VisualElement filters = ShellRows.Section(excluded, "SECTION_EXCLUDED_FILTERS", false);
        foreach(Filter value in Enum.GetValues(typeof(Filter))) {
            Filter filter = value;
            // No Quartz key, so the label passes through untranslated.
            Controls.Toggle(filters, FilterLabel(filter), QuartzShell.Writes(() => C.SimpleFilterExcludeList.Contains(filter), v => {
                if(!v) C.SimpleFilterExcludeList.Remove(filter);
                else if(!C.SimpleFilterExcludeList.Contains(filter)) C.SimpleFilterExcludeList.Add(filter);
                Save();
            }), o => { o.Id = $"FXRM_S_FILTER_EX_{filter}"; o.Default = false; });
        }
        Flag(p, "FXRM_S_ADVFILTER", c => c.SimpleAdvancedFilter, (c, v) => c.SimpleAdvancedFilter = v, Def.SimpleAdvancedFilter, "DESC_FXRM_S_ADVFILTER");
        Flag(p, "FXRM_S_BLOOM", c => c.SimpleBloom, (c, v) => c.SimpleBloom = v, Def.SimpleBloom, "DESC_FXRM_S_BLOOM");
        Flag(p, "FXRM_S_FLASH", c => c.SimpleFlash, (c, v) => c.SimpleFlash = v, Def.SimpleFlash, "DESC_FXRM_S_FLASH");
        Flag(p, "FXRM_S_HOM", c => c.SimpleHallOfMirrors, (c, v) => c.SimpleHallOfMirrors = v, Def.SimpleHallOfMirrors, "DESC_FXRM_S_HOM");
        Flag(p, "FXRM_S_SHAKE", c => c.SimpleScreenShake, (c, v) => c.SimpleScreenShake = v, Def.SimpleScreenShake, "DESC_FXRM_S_SHAKE");
        Flag(p, "FXRM_S_SCREENTILE", c => c.SimpleScreenTile, (c, v) => c.SimpleScreenTile = v, Def.SimpleScreenTile, "DESC_FXRM_S_SCREENTILE");
        Controls.Slider(p, "FXRM_S_MOVEMAX", QuartzShell.Writes(() => (float)C.SimpleMoveTrackMax, v => { C.SimpleMoveTrackMax = Mathf.RoundToInt(v); Save(); }), o => {
            o.Id = "FXRM_S_MOVEMAX";
            o.Min = 5f;
            o.Max = EffectRemoverSettings.MoveTrackUpperBound + 5f;
            o.Step = 5f;
            o.Format = "0";
            o.Default = Def.SimpleMoveTrackMax;
            o.Tooltip = "DESC_FXRM_S_MOVEMAX";
        });
        return null;
    }
    // The game names filters in its own language, which Facet can draw when it is English, or when its script
    // is the one Quartz's language draws (Korean, Chinese, Japanese). Otherwise the enum name: the game has
    // no English lookup short of switching its language.
    internal static string FilterLabel(Filter f) =>
        RDString.language == SystemLanguage.English || RDString.language switch {
            SystemLanguage.Korean => "ko",
            SystemLanguage.ChineseSimplified or SystemLanguage.ChineseTraditional or SystemLanguage.Chinese => "zh",
            SystemLanguage.Japanese => "ja",
            _ => null
        } == QuartzHost.LanguageCode(MainCore.Tr.Language)
            ? RDString.GetEnumValue(f)
            : Regex.Replace(f.ToString(), "(?<=[a-z])(?=[A-Z0-9])", " ");
    // Returns the Decoration Types wrapper, shown only while Decoration is on.
    private static VisualElement Enhanced(VisualElement body) {
        Flag(Controls.Card(body, null), "FXRM_ENABLE_SAVE", c => c.EnableSave, (c, v) => { c.EnableSave = v; EffectRemover.RefreshEditorSaveButtons(); },
            Def.EnableSave, "DESC_FXRM_ENABLE_SAVE");
        VisualElement events = Controls.Card(body, "HEADING_NON_DLC_EVENTS");
        Flag(events, "FXRM_FILTERS", c => c.Filters, (c, v) => c.Filters = v, Def.Filters);
        Flag(events, "FXRM_ADVFILTERS", c => c.AdvancedFilters, (c, v) => c.AdvancedFilters = v, Def.AdvancedFilters);
        Flag(events, "FXRM_DECORATIONS", c => c.Decorations, (c, v) => c.Decorations = v, Def.Decorations);
        Flag(events, "FXRM_BACKGROUNDS", c => c.Backgrounds, (c, v) => c.Backgrounds = v, Def.Backgrounds);
        Flag(events, "FXRM_CAMERAS", c => c.Cameras, (c, v) => c.Cameras = v, Def.Cameras);
        Flag(events, "FXRM_REPEAT", c => c.RepeatEvents, (c, v) => c.RepeatEvents = v, Def.RepeatEvents);
        Flag(events, "FXRM_FRAMERATE", c => c.FrameRate, (c, v) => c.FrameRate = v, Def.FrameRate);
        Flag(events, "FXRM_HITSOUNDS", c => c.HitSounds, (c, v) => c.HitSounds = v, Def.HitSounds);
        VisualElement planet = ShellRows.Section(events, "SECTION_PLANET_EVENTS", false);
        var planetRows = new Control<bool>[3];
        planetRows[0] = Flag(planet, "FXRM_PLANET_ORBIT", c => c.PlanetOrbit, (c, v) => c.PlanetOrbit = v, Def.PlanetOrbit);
        planetRows[1] = Flag(planet, "FXRM_PLANET_SCALE", c => c.PlanetScale, (c, v) => c.PlanetScale = v, Def.PlanetScale);
        planetRows[2] = Flag(planet, "FXRM_PLANET_RADIUS", c => c.PlanetRadius, (c, v) => c.PlanetRadius = v, Def.PlanetRadius);
        ToggleAll(planet, "FXRM_PLANET_ALL", planetRows);
        VisualElement track = ShellRows.Section(events, "SECTION_TRACK_EVENTS", false);
        var trackRows = new Control<bool>[4];
        trackRows[0] = Flag(track, "FXRM_TRACK_ANIMS", c => c.TrackAnimations, (c, v) => c.TrackAnimations = v, Def.TrackAnimations);
        trackRows[1] = Flag(track, "FXRM_TRACK_MOVES", c => c.TrackMoves, (c, v) => c.TrackMoves = v, Def.TrackMoves);
        trackRows[2] = Flag(track, "FXRM_TRACK_POSITIONS", c => c.TrackPositions, (c, v) => c.TrackPositions = v, Def.TrackPositions);
        trackRows[3] = Flag(track, "FXRM_TRACK_COLORS", c => c.TrackColors, (c, v) => c.TrackColors = v, Def.TrackColors);
        ToggleAll(track, "FXRM_TRACK_ALL", trackRows);
        VisualElement dlc = Controls.Card(body, "HEADING_DLC_EVENTS");
        Flag(dlc, "FXRM_HOLDSOUNDS", c => c.HoldSounds, (c, v) => c.HoldSounds = v, Def.HoldSounds);
        Flag(dlc, "FXRM_HIDEICONS", c => c.HideIcons, (c, v) => c.HideIcons = v, Def.HideIcons);
        VisualElement misc = Controls.Card(body, "HEADING_MISC");
        VisualElement decoTypes = Ui.Box(misc);
        VisualElement types = ShellRows.Section(decoTypes, "SECTION_DECORATION_TYPES", false);
        var typeRows = new Control<bool>[6];
        typeRows[0] = Flag(types, "FXRM_DECO_PLANET", c => c.DecoPlanet, (c, v) => c.DecoPlanet = v, Def.DecoPlanet);
        typeRows[1] = Flag(types, "FXRM_DECO_TILES", c => c.DecoTiles, (c, v) => c.DecoTiles = v, Def.DecoTiles);
        typeRows[2] = Flag(types, "FXRM_DECO_IMAGE", c => c.DecoImage, (c, v) => c.DecoImage = v, Def.DecoImage);
        typeRows[3] = Flag(types, "FXRM_DECO_TEXT", c => c.DecoText, (c, v) => c.DecoText = v, Def.DecoText);
        typeRows[4] = Flag(types, "FXRM_PARTICLES", c => c.Particles, (c, v) => c.Particles = v, Def.Particles);
        typeRows[5] = Flag(types, "FXRM_DECO_FAILHITBOX", c => c.DecoFailHitbox, (c, v) => c.DecoFailHitbox = v, Def.DecoFailHitbox,
            "DESC_FXRM_DECO_FAILHITBOX");
        ToggleAll(types, "FXRM_DECO_TYPES_ALL", typeRows);
        Flag(misc, "FXRM_REMOVE_ALL_DECO", c => c.RemoveAllDecorations, (c, v) => c.RemoveAllDecorations = v, Def.RemoveAllDecorations,
            "DESC_FXRM_REMOVE_ALL_DECO", () => C.Decorations);
        Flag(misc, "FXRM_LIMIT_OPACITY", c => c.LimitTrackOpacity, (c, v) => c.LimitTrackOpacity = v, Def.LimitTrackOpacity);
        Flag(misc, "FXRM_SET_ZOOM", c => c.SetCameraZoom, (c, v) => c.SetCameraZoom = v, Def.SetCameraZoom, null, () => C.Cameras);
        Controls.Slider(misc, "FXRM_ZOOM_SCALE", QuartzShell.Writes(() => C.CameraZoomScale, v => { C.CameraZoomScale = v; Save(); }), o => {
            o.Id = "FXRM_ZOOM_SCALE";
            o.Min = 100f;
            o.Max = 1000f;
            o.Step = 1f;
            o.Format = "0' %'";
            o.Default = Def.CameraZoomScale;
            o.VisibleWhen = () => C.Cameras && C.SetCameraZoom;
        });
        Flag(misc, "FXRM_RESET_ANIM", c => c.ResetTrackAnimation, (c, v) => c.ResetTrackAnimation = v, Def.ResetTrackAnimation,
            null, () => C.TrackAnimations);
        Flag(misc, "FXRM_RESET_COLOR", c => c.ResetTrackColor, (c, v) => c.ResetTrackColor = v, Def.ResetTrackColor,
            null, () => C.TrackColors);
        Flag(misc, "FXRM_TUTORIAL_PATTERNS", c => c.RemoveTutorialPatterns, (c, v) => c.RemoveTutorialPatterns = v, Def.RemoveTutorialPatterns,
            "DESC_FXRM_TUTORIAL_PATTERNS", () => C.Backgrounds);
        return decoTypes;
    }
}
#endif

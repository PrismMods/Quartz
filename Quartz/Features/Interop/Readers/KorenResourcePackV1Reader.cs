using System.Collections;
using System.Xml.Linq;
using Quartz.Core;
using Quartz.Features.Combo;
using Quartz.Features.EffectRemover;
using Quartz.Features.Judgement;
using Quartz.Features.PlanetColors;
using Quartz.Features.ProgressBar;
using Quartz.Features.Restriction;
using Quartz.Features.Tweaks;
using UnityEngine;
using static Quartz.Features.Interop.ReflectionHelpers;
using static Quartz.Features.Interop.Readers.KeyViewerImportShared;

namespace Quartz.Features.Interop.Readers;

// ===== KorenResourcePackV1 =====
//
// v1 is this mod's predecessor and v2 is a near-faithful rewrite of it, so
// almost every v1 field has a direct v2 home. v1 persists ONE flat
// `Settings : ModSettings` object whose field names equal its Settings.xml
// element names, so a single mapping body (ApplyV1Common) drives both the
// live-object path (reflection, primary) and the on-disk XML path (fallback)
// via a small reader abstraction.
internal static class KorenResourcePackV1Reader {
    private sealed class V1Reader {
        public Func<string, object> Scalar;    // bool/int/float/string leaf
        public Func<string, int[]> Keys;       // positional, length preserved, per-slot normalized
        public Func<string, string[]> Labels;  // positional labels
    }

    public static int ImportKorenResourcePackV1(
        SettingsImportOption option,
        SettingsImportReplaceMode keyViewerMode,
        SettingsImportKeyViewerPart keyViewerParts
    ) {
        int count = 0;

        object live = GetStaticMember(SettingsImporter.FindType(option, "KorenResourcePack.Main"), "settings");
        if(live != null) {
            count += ApplyV1Common(V1FromObject(live), keyViewerMode, keyViewerParts);
            count += ImportV1UiHider(live); // nested profile objects — reflection only
        }

        // Fall back to the on-disk Settings.xml only if nothing came through live
        // (e.g. the mod's static settings weren't reachable).
        if(count == 0) {
            XDocument doc = LoadXml(option, "Settings.xml");
            if(doc?.Root != null) count += ApplyV1Common(V1FromXml(doc.Root), keyViewerMode, keyViewerParts);
        }

        return count;
    }

    private static V1Reader V1FromObject(object live) => new() {
        Scalar = name => GetMemberValue(live, name),
        Keys = name => ReadPositionalKeys(GetMemberValue(live, name)),
        Labels = name => ReadStringArray(GetMemberValue(live, name)),
    };

    private static V1Reader V1FromXml(XElement root) => new() {
        Scalar = name => FindFirstDescendant(root, name)?.Value,
        Keys = name => ReadPositionalKeysXml(root, name),
        Labels = name => ReadPositionalLabelsXml(root, name),
    };

    private static int ApplyV1Common(
        V1Reader r,
        SettingsImportReplaceMode keyViewerMode,
        SettingsImportKeyViewerPart keyViewerParts
    ) {
        int count = 0;

        // --- ChatterBlocker ---
        if(TryConvertBool(r.Scalar("KCBOn"), out bool kcbOn)) {
            Features.ChatterBlocker.ChatterBlocker.EnsureConf();
            Features.ChatterBlocker.ChatterBlocker.Conf.Enabled = kcbOn;
            count++;
        }
        if(TryConvertFloat(r.Scalar("KCBThresholdMs"), out float kcbMs)) {
            Features.ChatterBlocker.ChatterBlocker.EnsureConf();
            Features.ChatterBlocker.ChatterBlocker.Conf.ThresholdMs = Mathf.Max(0f, kcbMs);
            count++;
        }

        // --- KeyLimiter ---
        if(TryConvertBool(r.Scalar("KeyLimiterOn"), out bool klOn)) {
            Features.KeyLimiter.KeyLimiter.EnsureConf();
            Features.KeyLimiter.KeyLimiter.Conf.Enabled = klOn;
            count++;
        }
        int[] klKeys = r.Keys("KeyLimiterAllowed");
        if(klKeys is { Length: > 0 }) {
            Features.KeyLimiter.KeyLimiter.SetAllowedKeys(klKeys);
            count++;
        }

        // --- KeyViewer (simple) ---
        ImportedKeyViewer kv = ReadKeyViewerFromV1(r);
        if(kv.Available != SettingsImportKeyViewerPart.None) {
            count += ApplyKeyViewerImport(kv, keyViewerMode, keyViewerParts);
        }

        // --- Combo ---
        ComboOverlay.EnsureConf();
        if(TryConvertBool(r.Scalar("comboOn"), out bool comboOn)) { ComboOverlay.Conf.Enabled = comboOn; count++; }
        if(TryConvertBool(r.Scalar("EnableAutoCombo"), out bool autoCombo)) { ComboOverlay.Conf.CountAuto = autoCombo; count++; }
        if(TryConvertInt(r.Scalar("ComboColorMax"), out int comboMax)) { ComboOverlay.Conf.ColorMax = comboMax; count++; }
        if(TryConvertBool(r.Scalar("XPerfectComboEnabled"), out bool xperfCombo)) { ComboOverlay.Conf.XPerfectComboEnabled = xperfCombo; count++; }
        if(V1Color(r, "ComboColorLow") is { } comboLow) { ComboOverlay.Conf.SetColorLow(comboLow); count++; }
        if(V1Color(r, "ComboColorHigh") is { } comboHigh) { ComboOverlay.Conf.SetColorHigh(comboHigh); count++; }

        // --- Judgement ---
        JudgementOverlay.EnsureConf();
        if(TryConvertBool(r.Scalar("judgementOn"), out bool judgeOn)) { JudgementOverlay.Conf.Enabled = judgeOn; count++; }
        // v1 drives the judgement position purely from judgementPositionY; its
        // LocationUp flag is set in the old UI but never read by the renderer, so
        // it's deliberately ignored here rather than mapped to a phantom shift.
        if(TryConvertFloat(r.Scalar("judgementPositionY"), out float judgeY)) { JudgementOverlay.Conf.OffsetY = judgeY; count++; }
        if(TryConvertFloat(r.Scalar("judgementSize"), out float judgeSize)) { JudgementOverlay.Conf.Size = judgeSize; count++; }
        if(TryConvertFloat(r.Scalar("judgementSpacing"), out float judgeSpace)) { JudgementOverlay.Conf.Spacing = judgeSpace; count++; }

        // --- ProgressBar ---
        ProgressBarOverlay.EnsureConf();
        if(TryConvertBool(r.Scalar("progressBarOn"), out bool pbOn)) { ProgressBarOverlay.Conf.Enabled = pbOn; count++; }
        if(V1Color(r, "ProgressBarFill") is { } pbFill) { ProgressBarOverlay.Conf.SetFillColor(pbFill); count++; }
        if(V1Color(r, "ProgressBarBack") is { } pbBack) { ProgressBarOverlay.Conf.SetBackColor(pbBack); count++; }
        if(V1Color(r, "ProgressBarBorder") is { } pbBorder) { ProgressBarOverlay.Conf.SetOutlineColor(pbBorder); count++; }

        // --- Otto icon ---
        Features.OttoIcon.OttoIcon.EnsureConf();
        if(TryConvertBool(r.Scalar("ChangeOttoIcon"), out bool ottoOn)) { Features.OttoIcon.OttoIcon.Conf.Enabled = ottoOn; count++; }
        if(V1Color(r, "Otto") is { } ottoColor) { Features.OttoIcon.OttoIcon.Conf.SetColor(ottoColor); count++; }
        if(TryConvertFloat(r.Scalar("OttoOffsetX"), out float ottoX)) { Features.OttoIcon.OttoIcon.Conf.OffsetX = ottoX; count++; }
        if(TryConvertFloat(r.Scalar("OttoOffsetY"), out float ottoY)) { Features.OttoIcon.OttoIcon.Conf.OffsetY = ottoY; count++; }

        // --- Planet (ball/ring) colors ---
        Features.PlanetColors.PlanetColors.EnsureConf();
        PlanetColorsSettings planet = Features.PlanetColors.PlanetColors.Conf;
        if(TryConvertBool(r.Scalar("ChangeBallColor"), out bool ballOn)) { planet.Enabled = ballOn; count++; }
        for(int slot = 0; slot < 3; slot++) {
            string prefix = "BallPlanet" + (slot + 1);
            if(V1Color(r, prefix) is { } ballColor) { planet.SetBallRgb(slot, ballColor); count++; }
            if(TryConvertFloat(r.Scalar(prefix + "Opacity"), out float ballOp)) { planet.BallOpacity[slot] = Mathf.Clamp01(ballOp); count++; }
        }
        if(TryConvertBool(r.Scalar("ChangeRingColor"), out bool ringOn)) { planet.EnableRingRecolor = ringOn; count++; }
        if(V1Color(r, "Ring") is { } ringColor) { planet.SetRingRgb(ringColor); count++; }

        // --- Judgement restriction + death limit (field names line up directly) ---
        Features.Restriction.Restriction.EnsureConf();
        RestrictionSettings restrict = Features.Restriction.Restriction.Conf;
        if(TryConvertBool(r.Scalar("JRestrictOn"), out bool jrOn)) { restrict.JRestrictEnabled = jrOn; count++; }
        if(TryConvertInt(r.Scalar("JRestrictMode"), out int jrMode)) { restrict.JRestrictMode = jrMode; count++; }
        if(TryConvertFloat(r.Scalar("JRestrictAccuracy"), out float jrAcc)) { restrict.JRestrictAccuracy = jrAcc; count++; }
        if(TryConvertInt(r.Scalar("JRestrictAllowedMask"), out int jrMask)) { restrict.JRestrictAllowedMask = jrMask; count++; }
        if(TryConvertBool(r.Scalar("DeathLimitOn"), out bool dlOn)) { restrict.DeathLimitEnabled = dlOn; count++; }
        if(TryConvertBool(r.Scalar("DeathLimitMaxDeathsOn"), out bool dlDeathsOn)) { restrict.MaxDeathsOn = dlDeathsOn; count++; }
        if(TryConvertInt(r.Scalar("DeathLimitMaxDeaths"), out int dlDeaths)) { restrict.MaxDeaths = dlDeaths; count++; }
        if(TryConvertBool(r.Scalar("DeathLimitMaxMissesOn"), out bool dlMissOn)) { restrict.MaxMissesOn = dlMissOn; count++; }
        if(TryConvertInt(r.Scalar("DeathLimitMaxMisses"), out int dlMiss)) { restrict.MaxMisses = dlMiss; count++; }
        if(TryConvertBool(r.Scalar("DeathLimitMaxOverloadsOn"), out bool dlOverOn)) { restrict.MaxOverloadsOn = dlOverOn; count++; }
        if(TryConvertInt(r.Scalar("DeathLimitMaxOverloads"), out int dlOver)) { restrict.MaxOverloads = dlOver; count++; }

        // --- Tweaks (the toggles v2 still has) ---
        Features.Tweaks.Tweaks.EnsureConf();
        TweaksSettings tweaks = Features.Tweaks.Tweaks.Conf;
        void TweakFlag(string name, Action<bool> set) {
            if(TryConvertBool(r.Scalar(name), out bool v)) { set(v); count++; }
        }
        TweakFlag("RemoveAllCheckpoints", v => tweaks.RemoveAllCheckpoints = v);
        TweakFlag("RemoveBallCoreParticles", v => tweaks.RemoveBallCoreParticles = v);
        TweakFlag("DisableTileHitGlow", v => tweaks.DisableTileHitGlow = v);
        TweakFlag("RemovePlanetGlow", v => tweaks.RemovePlanetGlow = v);
        TweakFlag("DisableAutoPause", v => tweaks.DisableAutoPause = v);
        TweakFlag("BlockMouseWheelScrollWhilePlaying", v => tweaks.BlockMouseWheelScrollWhilePlaying = v);

        // --- Effect remover (v1 names are the v2 ones, prefixed "EffectRemover") ---
        Features.EffectRemover.EffectRemover.EnsureConf();
        EffectRemoverSettings effect = Features.EffectRemover.EffectRemover.Conf;
        if(TryConvertBool(r.Scalar("EffectRemoverOn"), out bool erOn)) { effect.On = erOn; count++; }
        if(TryConvertFloat(r.Scalar("EffectRemoverCameraZoomScale"), out float erZoom)) { effect.CameraZoomScale = erZoom; count++; }
        void EffectFlag(string name, Action<bool> set) {
            if(TryConvertBool(r.Scalar(name), out bool v)) { set(v); count++; }
        }
        EffectFlag("EffectRemoverEnableSave", v => effect.EnableSave = v);
        EffectFlag("EffectRemoverResetTrackAnimation", v => effect.ResetTrackAnimation = v);
        EffectFlag("EffectRemoverResetTrackColor", v => effect.ResetTrackColor = v);
        EffectFlag("EffectRemoverRemoveAllDecorations", v => effect.RemoveAllDecorations = v);
        EffectFlag("EffectRemoverResetTrackOpacity", v => effect.LimitTrackOpacity = v);
        EffectFlag("EffectRemoverSetCameraZoom", v => effect.SetCameraZoom = v);
        EffectFlag("EffectRemoverFilters", v => effect.Filters = v);
        EffectFlag("EffectRemoverAdvancedFilters", v => effect.AdvancedFilters = v);
        EffectFlag("EffectRemoverParticles", v => effect.Particles = v);
        EffectFlag("EffectRemoverDecorations", v => effect.Decorations = v);
        EffectFlag("EffectRemoverBackgrounds", v => effect.Backgrounds = v);
        EffectFlag("EffectRemoverCameras", v => effect.Cameras = v);
        EffectFlag("EffectRemoverRepeatEvents", v => effect.RepeatEvents = v);
        EffectFlag("EffectRemoverFrameRate", v => effect.FrameRate = v);
        EffectFlag("EffectRemoverHitSounds", v => effect.HitSounds = v);
        EffectFlag("EffectRemoverPlanetOrbit", v => effect.PlanetOrbit = v);
        EffectFlag("EffectRemoverPlanetScale", v => effect.PlanetScale = v);
        EffectFlag("EffectRemoverPlanetRadius", v => effect.PlanetRadius = v);
        EffectFlag("EffectRemoverTrackAnimations", v => effect.TrackAnimations = v);
        EffectFlag("EffectRemoverTrackPositions", v => effect.TrackPositions = v);
        EffectFlag("EffectRemoverTrackMoves", v => effect.TrackMoves = v);
        EffectFlag("EffectRemoverTrackColors", v => effect.TrackColors = v);
        EffectFlag("EffectRemoverHoldSounds", v => effect.HoldSounds = v);
        EffectFlag("EffectRemoverHideIcons", v => effect.HideIcons = v);

        return count;
    }

    // UI hiding lives in nested profile objects (UiHidingPlayingProfile /
    // RecordingProfile), whose fields match v2's UiHiderProfile one-for-one — so
    // the AdofaiTweaks profile copier applies verbatim. Reflection only; the XML
    // fallback skips it.
    private static int ImportV1UiHider(object live) {
        Features.UiHider.UiHider.EnsureConf();
        int count = 0;
        count += ApplyAdofaiHideUiProfile(GetMemberValue(live, "UiHidingPlayingProfile"), Features.UiHider.UiHider.Conf.Playing);
        count += ApplyAdofaiHideUiProfile(GetMemberValue(live, "UiHidingRecordingProfile"), Features.UiHider.UiHider.Conf.Recording);

        if(TryGetBool(live, "UiHidingRecordingMode", out bool rec)) { Features.UiHider.UiHider.Conf.RecordingMode = rec; count++; }
        if(TryGetBool(live, "UiHidingUseRecordingModeShortcut", out bool useShortcut)) { Features.UiHider.UiHider.Conf.UseShortcut = useShortcut; count++; }

        if(TryGetBool(live, "UiHidingShortcutCtrl", out bool ctrl) && ctrl) {
            Features.UiHider.UiHider.Conf.ShortcutModifier = (int)Keybind.KeyModifier.Ctrl;
        } else if(TryGetBool(live, "UiHidingShortcutAlt", out bool alt) && alt) {
            Features.UiHider.UiHider.Conf.ShortcutModifier = (int)Keybind.KeyModifier.Alt;
        } else if(TryGetBool(live, "UiHidingShortcutShift", out bool shift) && shift) {
            Features.UiHider.UiHider.Conf.ShortcutModifier = (int)Keybind.KeyModifier.Shift;
        } else {
            Features.UiHider.UiHider.Conf.ShortcutModifier = (int)Keybind.KeyModifier.None;
        }
        if(TryGetInt(live, "UiHidingShortcutKey", out int key)) { Features.UiHider.UiHider.Conf.ShortcutKey = NormalizeKeyInt(key); count++; }

        if(TryGetBool(live, "UiHidingOn", out bool on)) { Features.UiHider.UiHider.Conf.Enabled = on; count++; }
        return count;
    }

    private static ImportedKeyViewer ReadKeyViewerFromV1(V1Reader r) {
        ImportedKeyViewer kv = new();

        if(TryConvertInt(r.Scalar("KeyViewerSimpleStyle"), out int style)) { kv.HasStyle = true; kv.Style = style; }
        kv.Key10 = r.Keys("KeyViewerSimpleKey10");
        kv.Key12 = r.Keys("KeyViewerSimpleKey12");
        kv.Key16 = r.Keys("KeyViewerSimpleKey16");
        kv.Key20 = r.Keys("KeyViewerSimpleKey20");

        // Foot keys: v1 keeps a separate array per foot style; v2 keeps one flat
        // FootKeys[16] + a style whose key-count is style*2. v1 styles 1-4 (2/4/
        // 6/8 keys) line up 1:1; v1 style 5 is the 16-key layout, which is v2's
        // style 8.
        if(TryConvertInt(r.Scalar("KeyViewerSimpleFootStyle"), out int footStyle)) {
            kv.HasFoot = true;
            kv.FootStyle = footStyle == 5 ? 8 : Mathf.Clamp(footStyle, 0, 4);
            string footField = footStyle switch {
                1 => "KeyViewerSimpleFootKey2",
                2 => "KeyViewerSimpleFootKey4",
                3 => "KeyViewerSimpleFootKey6",
                4 => "KeyViewerSimpleFootKey8",
                5 => "KeyViewerSimpleFootKey16",
                _ => null,
            };
            kv.FootKeys = footField == null ? null : r.Keys(footField);
        }

        // Ghost keys map array-for-array (v2 kept v1's per-style ghost layout).
        kv.GhostKey10 = r.Keys("KeyViewerSimpleGhostKey10");
        kv.GhostKey12 = r.Keys("KeyViewerSimpleGhostKey12");
        kv.GhostKey16 = r.Keys("KeyViewerSimpleGhostKey16");
        kv.GhostKey20 = r.Keys("KeyViewerSimpleGhostKey20");

        if(kv.HasStyle || AnyKeys(kv) || kv.HasFoot
            || AnyGhost(kv.GhostKey10, kv.GhostKey12, kv.GhostKey16, kv.GhostKey20)) {
            kv.Available |= SettingsImportKeyViewerPart.KeysLayout;
        }

        kv.Key10Text = r.Labels("KeyViewerSimpleKey10Text");
        kv.Key12Text = r.Labels("KeyViewerSimpleKey12Text");
        kv.Key16Text = r.Labels("KeyViewerSimpleKey16Text");
        kv.Key20Text = r.Labels("KeyViewerSimpleKey20Text");
        if(AnyLabels(kv)) {
            kv.Available |= SettingsImportKeyViewerPart.Labels;
        }

        kv.Bg = V1Color(r, "SKvBg");
        kv.BgClicked = V1Color(r, "SKvBgc");
        kv.Outline = V1Color(r, "SKvOut");
        kv.OutlineClicked = V1Color(r, "SKvOutc");
        kv.Text = V1Color(r, "SKvTxt");
        kv.TextClicked = V1Color(r, "SKvTxtc");
        kv.Rain = V1Color(r, "SKvRain");
        kv.Rain2 = V1Color(r, "SKvRain2");
        kv.Rain3 = V1Color(r, "SKvRain3");
        kv.GhostRain = V1Color(r, "SKvGhostRain");
        if(AnyColors(kv) || kv.GhostRain != null) {
            kv.Available |= SettingsImportKeyViewerPart.Colors;
        }

        if(TryConvertBool(r.Scalar("KeyViewerSimpleUseRain"), out bool useRain)) { kv.HasRainEnabled = true; kv.RainEnabled = useRain; }
        if(TryConvertFloat(r.Scalar("KeyViewerSimpleRainSpeed"), out float rainSpeed)) { kv.HasRainSpeed = true; kv.RainSpeed = rainSpeed; }
        if(TryConvertFloat(r.Scalar("KeyViewerSimpleRainHeight"), out float rainHeight)) { kv.HasRainHeight = true; kv.RainHeight = rainHeight; }
        if(kv.HasRainEnabled || kv.HasRainSpeed || kv.HasRainHeight) {
            kv.Available |= SettingsImportKeyViewerPart.Rain;
        }

        if(TryConvertFloat(r.Scalar("KeyViewerSimpleSize"), out float size)) { kv.HasSize = true; kv.Size = Mathf.Clamp(size, 0.1f, 3f); }
        if(kv.HasSize) {
            kv.Available |= SettingsImportKeyViewerPart.PositionSize;
        }

        if(TryConvertBool(r.Scalar("keyViewerOn"), out bool en)) { kv.HasEnabled = true; kv.Enabled = en; }
        if(TryConvertBool(r.Scalar("KeyViewerSimpleSyncToKeyLimiter"), out bool sync)) { kv.HasSync = true; kv.SyncToKeyLimiter = sync; }

        return kv;
    }

    private static bool AnyGhost(params int[][] arrays) {
        foreach(int[] arr in arrays) {
            if(arr != null && arr.Any(k => k != 0)) return true;
        }
        return false;
    }

    // Assemble a v2 Color from v1's flat per-channel float fields (prefix + R/G/
    // B/A). Alpha is optional — fields like the ball-planet colors store no A.
    private static Color? V1Color(V1Reader r, string prefix) {
        if(TryConvertFloat(r.Scalar(prefix + "R"), out float cr)
            && TryConvertFloat(r.Scalar(prefix + "G"), out float cg)
            && TryConvertFloat(r.Scalar(prefix + "B"), out float cb)) {
            float a = TryConvertFloat(r.Scalar(prefix + "A"), out float ca) ? ca : 1f;
            return new Color(Mathf.Clamp01(cr), Mathf.Clamp01(cg), Mathf.Clamp01(cb), Mathf.Clamp01(a));
        }
        return null;
    }

    // Positional key reader: unlike ReadKeyCodeEnumerable (a deduped SET, used
    // for allow-lists), these arrays are slot-indexed — a 0 means "no key for
    // this slot" and duplicates/positions must be preserved.
    private static int[] ReadPositionalKeys(object value) {
        if(value is not IEnumerable enumerable || value is string) return null;
        List<int> result = [];
        foreach(object item in enumerable) {
            result.Add(TryConvertKeyCode(item, out int key) ? key : 0);
        }
        return result.Count > 0 ? [.. result] : null;
    }

    private static int[] ReadPositionalKeysXml(XElement root, string name) {
        XElement list = FindFirstDescendant(root, name);
        if(list == null) return null;
        List<int> result = [];
        foreach(XElement item in list.Elements()) {
            result.Add(TryConvertKeyCode(item.Value, out int key) ? key : 0);
        }
        return result.Count > 0 ? [.. result] : null;
    }

    private static string[] ReadPositionalLabelsXml(XElement root, string name) {
        XElement list = FindFirstDescendant(root, name);
        if(list == null) return null;
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        List<string> result = [];
        foreach(XElement item in list.Elements()) {
            bool nil = string.Equals((string)item.Attribute(xsi + "nil"), "true", StringComparison.OrdinalIgnoreCase);
            result.Add(nil ? "" : item.Value);
        }
        return result.Count > 0 ? [.. result] : null;
    }
}

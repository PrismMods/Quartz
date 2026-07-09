using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;

namespace Quartz.Features.KeyViewer;

public sealed partial class KeyViewerSettings : ISettingsFile {
    public const string ModeSimple = "simple";
    public const string ModeDmNote = "dmnote";

    public bool Enabled = true;

    // Keep the viewer on screen in menus and outside of gameplay, not just
    // while a level is playing. Default on so it behaves like a static HUD.
    public bool ShowOutsideGame = true;

    // Renderer mode (v1 KeyViewerMode): "simple" = the key grid, "dmnote" =
    // the DM-note preset renderer.
    public string Mode = ModeSimple;

    // 0 = 10 keys, 1 = 12, 2 = 16, 3 = 20, 4 = 8, 5 = 14. The first four match
    // v1's KeyViewerSimpleStyle; 8 and 14 are v2 additions appended at the end
    // so existing saved Style ints keep their meaning.
    public int Style = 2;
    public const int MaxStyle = 5;

    public bool IsSimpleMode => string.Equals(Mode, ModeSimple, StringComparison.OrdinalIgnoreCase);
    public bool IsDmNoteMode => string.Equals(Mode, ModeDmNote, StringComparison.OrdinalIgnoreCase);

    public float Size = 0.8f;
    public float OffsetX = -713.51886f;
    public float OffsetY = 24.76001f;

    // Keep the Key Limiter's allowed list matched to the keys on the viewer
    // (v1 KeyViewerSimpleSyncToKeyLimiter, default on).
    public bool SyncToKeyLimiter = true;

    // Rain effect (v1 KeyViewerSimpleUseRain + SKvRain* defaults). Width 0
    // means "use the key's width"; rain 2 covers the second/third row groups.
    public bool RainEnabled = true;
    public float RainSpeed = 450f;
    public float RainHeight = 300f;
    public float RainFade = 60f;
    // Rain streak width per row group; 0 = match the key's width. A positive
    // value is per key-column, so a 2-wide key (e.g. the 10-key's bottom row)
    // gets 2x that width.
    public float RainWidth = 0f;
    public float Rain2Width = 40f;
    public float RainOffsetY = 0f;
    public float Rain2OffsetY = 0f;

    // KPS/Total placement on the back row: false = far apart (one on each
    // side, the v1 default), true = side by side in the centre.
    public bool StatsTogether = true;

    // Counter display extras (v1 simple-mode options).
    // CountFormatting: thousands separators on the counters (1,234).
    // HideMainKeyCount: hide the per-key counters on the key boxes.
    // PerKeyKps: the key boxes' counters show that key's KPS instead of its
    //   cumulative press count.
    // StreamerMode: hide the KPS and Total stat boxes entirely.
    public bool CountFormatting = false;
    public bool HideMainKeyCount = false;
    public bool PerKeyKps = false;
    public bool StreamerMode = false;

    public float RainR = 1f, RainG = 0f, RainB = 0f, RainA = 1f;
    public float Rain2R = 1f, Rain2G = 1f, Rain2B = 1f, Rain2A = 1f;
    public float Rain3R = 1f, Rain3G = 0f, Rain3B = 1f, Rain3A = 1f;

    // DM Note renderer settings from KorenResourcePack's KeyViewerMode =
    // "dmnote". PresetJson is the exported preset payload; SelectedTab picks
    // the key type inside it (for example "4key").
    public string DmPresetJson = "";
    public string DmSelectedTab = "4key";
    public float DmOffsetX = 0f;
    public float DmOffsetY = 240f;
    public float DmScale = 1f;
    public bool DmNoteEffect = true;
    public float DmNoteSpeed = 1000f;
    public float DmTrackHeight = 200f;
    public bool DmNoteReverse = false;
    public bool DmShowCounter = true;
    // KRP v2's single edge-fade value. The split fade fields are kept only so
    // configs saved by earlier builds can migrate without losing the value.
    public float DmFadePx = 60f;
    public float DmFadeTopPx = 60f;
    public float DmFadeBottomPx = 0f;
    public float DmReverseFadeTopPx = 0f;
    public float DmReverseFadeBottomPx = 60f;
    public bool DmDelayedNoteEnabled = false;
    public float DmShortNoteThresholdMs = 50f;
    public float DmShortNoteMinLengthPx = 30f;
    public float DmKeyDisplayDelayMs = 0f;
    // 0 = hide blocked keys, 1 = rain only, 2 = full press.
    public int DmOutOfLimiterMode = 1;

    // Custom CSS layer for the DM Note renderer. DmCssText holds the imported
    // stylesheet verbatim (like DmPresetJson, so it travels with the config);
    // DmCssPath remembers where it came from for the page status / re-import.
    // The CSS is parsed by KeyViewerStylesheet and layered over the preset's
    // per-key styling (colors, border, radius, font, glow, animated gradients).
    public bool DmCssEnabled = false;
    public string DmCssText = "";
    public string DmCssPath = "";

    // Key codes per style, stored as KeyCode ints like v1.
    public int[] Key10 = [113, 51, 52, 116, 111, 45, 61, 92, 32, 104];
    public int[] Key12 = [113, 51, 52, 116, 111, 45, 61, 92, 32, 98, 104, 46];
    public int[] Key16 = [113, 51, 52, 116, 111, 45, 61, 92, 32, 98, 104, 46, 97, 304, 273, 13];
    public int[] Key20 = [113, 51, 52, 116, 111, 45, 61, 92, 32, 98, 104, 44, 97, 304, 303, 13, 110, 103, 109, 107];
    // v2 additions: 8 = the front row only; 14 = front row + a centred 6-key
    // back row. Defaults reuse the matching prefixes of Key10/Key16.
    public int[] Key8 = [113, 51, 52, 116, 111, 45, 61, 92];
    public int[] Key14 = [113, 51, 52, 116, 111, 45, 61, 92, 32, 98, 104, 46, 97, 304];

    // Per-slot label overrides (v1 KeyViewerSimpleKey*Text); empty = derive
    // the caption from the key code.
    public string[] Key10Text = new string[10];
    public string[] Key12Text = new string[12];
    public string[] Key16Text = new string[16];
    public string[] Key20Text = new string[20];
    public string[] Key8Text = new string[8];
    public string[] Key14Text = new string[14];

    // Box colors, idle and pressed (v1 SKvBg/SKvBgc/SKvOut/SKvOutc/SKvTxt/SKvTxtc).
    public float BgR = 1f, BgG = 0.2352941f, BgB = 0.2352941f, BgA = 0.1960784f;
    public float BgPressedR = 1f, BgPressedG = 1f, BgPressedB = 1f, BgPressedA = 1f;
    public float OutlineR = 1f, OutlineG = 0f, OutlineB = 0f, OutlineA = 1f;
    public float OutlinePressedR = 1f, OutlinePressedG = 1f, OutlinePressedB = 1f, OutlinePressedA = 1f;
    public float TextR = 1f, TextG = 1f, TextB = 1f, TextA = 1f;
    public float TextPressedR = 0f, TextPressedG = 0f, TextPressedB = 0f, TextPressedA = 1f;

    // Per-key overrides use a flat slot model, like v1: 0-19 = main keys,
    // 20-35 = foot keys. A style only fills the slots it actually has.
    public const int SlotCount = 36;
    public const int FootSlotBase = 20;

    // Font sizes: global multipliers (v1 KeyFontSize / CounterFontSize) with
    // optional per-key overrides. The opt-in is PER SLOT: a slot uses its
    // PerKeyKeyFont/PerKeyCounterFont only when PerKeyFontEnabled[slot] is set,
    // otherwise the shared scale. PerKeyFontInit records whether a slot was ever
    // seeded from the shared scale, so the first opt-in matches the current look
    // instead of snapping to 1x while a later off/on keeps the slot's edits.
    // (v1 had ONE global flag; Quartz makes it per slot so one key can differ
    // without flipping the rest.)
    public float KeyFontScale = 1f;
    public float CounterFontScale = 1f;
    public bool[] PerKeyFontEnabled = new bool[SlotCount];
    public bool[] PerKeyFontInit = new bool[SlotCount];
    public float[] PerKeyKeyFont = Filled(SlotCount, 1f);
    public float[] PerKeyCounterFont = Filled(SlotCount, 1f);

    // Per-key colors (v1 EnablePerKeyColors + PerKey* arrays). Each slot keeps
    // an idle/pressed pair for background, outline and text, plus a rain colour.
    // The opt-in is PER SLOT (PerKeyColorEnabled[slot]) like the fonts above —
    // enabling one key never touches the others; PerKeyColorInit tracks the
    // first-seed-from-global per slot. Defaults match the global colours so a
    // freshly-seeded slot looks identical until edited.
    public bool[] PerKeyColorEnabled = new bool[SlotCount];
    public bool[] PerKeyColorInit = new bool[SlotCount];
    public Color[] PerKeyBg = FilledColor(SlotCount, new Color(1f, 0.2352941f, 0.2352941f, 0.1960784f));
    public Color[] PerKeyBgPressed = FilledColor(SlotCount, new Color(1f, 1f, 1f, 1f));
    public Color[] PerKeyOutline = FilledColor(SlotCount, new Color(1f, 0f, 0f, 1f));
    public Color[] PerKeyOutlinePressed = FilledColor(SlotCount, new Color(1f, 1f, 1f, 1f));
    public Color[] PerKeyText = FilledColor(SlotCount, new Color(1f, 1f, 1f, 1f));
    public Color[] PerKeyTextPressed = FilledColor(SlotCount, new Color(0f, 0f, 0f, 1f));
    public Color[] PerKeyRain = FilledColor(SlotCount, new Color(1f, 0f, 0f, 1f));

    // Foot keys: a SEPARATE draggable overlay element with its own position,
    // moved independently of the main grid in Reorganize mode (FootOffsetX/Y is
    // that element's position, not an offset from the main grid). FootStyle
    // 0 = none, 1..8 = 2/4/6/8/10/12/14/16 keys. They share the flat per-key
    // slot space at FootSlotBase (20-35), light on press, don't add to counters.
    public int FootStyle = 0;
    // Default position sits beside (to the right of) the main grid at the same
    // height, so a freshly-enabled foot row is easy to find before dragging.
    public float FootOffsetX = -360f;
    public float FootOffsetY = 24.76001f;
    public int[] FootKeys = [289, 285, 288, 284, 287, 283, 286, 282, 48, 54, 57, 53, 56, 52, 55, 51];
    public string[] FootKeysText = new string[16];
    public int FootKeyCount() => Mathf.Clamp(FootStyle, 0, 8) * 2;

    // Ghost rain: an optional secondary key per slot that emits its own
    // ghost-coloured rain streak without touching the press counters
    // (v1 KeyViewerSimpleGhost* + SKvGhostRain*). 0 = no ghost key for a slot;
    // a slot's ghost rain is active simply by having a ghost key set (no
    // separate enable flag).
    public float GhostRainR = 1f, GhostRainG = 0f, GhostRainB = 0f, GhostRainA = 0.45f;
    // Dotted ghost rain (port of JipperResourcePack's tiled ghost-rain
    // sprite): draws the ghost streak as a repeating dash/gap pattern
    // instead of one solid quad. Off by default to match existing behavior.
    public bool GhostRainDotted = false;
    public float GhostRainDotLength = 10f;
    public float GhostRainGapLength = 6f;
    public int[] GhostKey8 = new int[8];
    public int[] GhostKey10 = new int[10];
    public int[] GhostKey12 = new int[12];
    public int[] GhostKey14 = new int[14];
    public int[] GhostKey16 = new int[16];
    public int[] GhostKey20 = new int[20];

    // Per-key press counts, keyed by upper-case KeyCode name. v1 kept these
    // in PlayerPrefs ("kvkey_*"); v2 keeps them with the rest of the config.
    public Dictionary<string, int> Counts = new(StringComparer.OrdinalIgnoreCase);

    public int[] KeysForStyle(int style) => style switch {
        0 => Key10,
        1 => Key12,
        3 => Key20,
        4 => Key8,
        5 => Key14,
        _ => Key16,
    };

    public string[] LabelsForStyle(int style) => style switch {
        0 => Key10Text,
        1 => Key12Text,
        3 => Key20Text,
        4 => Key8Text,
        5 => Key14Text,
        _ => Key16Text,
    };

    public int[] GhostKeysForStyle(int style) => style switch {
        0 => GhostKey10,
        1 => GhostKey12,
        3 => GhostKey20,
        4 => GhostKey8,
        5 => GhostKey14,
        _ => GhostKey16,
    };

    public Color GetGhostRain() => IOUtils.Rgba(GhostRainR, GhostRainG, GhostRainB, GhostRainA);
    public void SetGhostRain(Color c) => IOUtils.SetRgba(c, ref GhostRainR, ref GhostRainG, ref GhostRainB, ref GhostRainA);

    public Color GetBg() => IOUtils.Rgba(BgR, BgG, BgB, BgA);
    public void SetBg(Color c) => IOUtils.SetRgba(c, ref BgR, ref BgG, ref BgB, ref BgA);

    public Color GetBgPressed() => IOUtils.Rgba(BgPressedR, BgPressedG, BgPressedB, BgPressedA);
    public void SetBgPressed(Color c) => IOUtils.SetRgba(c, ref BgPressedR, ref BgPressedG, ref BgPressedB, ref BgPressedA);

    public Color GetOutline() => IOUtils.Rgba(OutlineR, OutlineG, OutlineB, OutlineA);
    public void SetOutline(Color c) => IOUtils.SetRgba(c, ref OutlineR, ref OutlineG, ref OutlineB, ref OutlineA);

    public Color GetOutlinePressed() => IOUtils.Rgba(OutlinePressedR, OutlinePressedG, OutlinePressedB, OutlinePressedA);
    public void SetOutlinePressed(Color c) => IOUtils.SetRgba(c, ref OutlinePressedR, ref OutlinePressedG, ref OutlinePressedB, ref OutlinePressedA);

    public Color GetText() => IOUtils.Rgba(TextR, TextG, TextB, TextA);
    public void SetText(Color c) => IOUtils.SetRgba(c, ref TextR, ref TextG, ref TextB, ref TextA);

    public Color GetTextPressed() => IOUtils.Rgba(TextPressedR, TextPressedG, TextPressedB, TextPressedA);
    public void SetTextPressed(Color c) => IOUtils.SetRgba(c, ref TextPressedR, ref TextPressedG, ref TextPressedB, ref TextPressedA);

    public Color GetRain() => IOUtils.Rgba(RainR, RainG, RainB, RainA);
    public void SetRain(Color c) => IOUtils.SetRgba(c, ref RainR, ref RainG, ref RainB, ref RainA);

    public Color GetRain2() => IOUtils.Rgba(Rain2R, Rain2G, Rain2B, Rain2A);
    public void SetRain2(Color c) => IOUtils.SetRgba(c, ref Rain2R, ref Rain2G, ref Rain2B, ref Rain2A);

    public Color GetRain3() => IOUtils.Rgba(Rain3R, Rain3G, Rain3B, Rain3A);
    public void SetRain3(Color c) => IOUtils.SetRgba(c, ref Rain3R, ref Rain3G, ref Rain3B, ref Rain3A);

    // Per-key resolvers: the slot's override when that slot has opted in and is
    // in range, otherwise the matching global value.
    public Color PerKeyOr(Color[] arr, int slot, Color global) =>
        arr != null && slot >= 0 && slot < arr.Length
        && slot < PerKeyColorEnabled.Length && PerKeyColorEnabled[slot]
            ? arr[slot] : global;

    public float KeyFontFor(int slot) =>
        slot >= 0 && slot < PerKeyKeyFont.Length
        && slot < PerKeyFontEnabled.Length && PerKeyFontEnabled[slot]
            ? PerKeyKeyFont[slot] : KeyFontScale;

    public float CounterFontFor(int slot) =>
        slot >= 0 && slot < PerKeyCounterFont.Length
        && slot < PerKeyFontEnabled.Length && PerKeyFontEnabled[slot]
            ? PerKeyCounterFont[slot] : CounterFontScale;

    // Copy the current global colours / font scales into every slot. Used to
    // seed the per-key arrays the first time the user enables them so the view
    // doesn't jump, and from the page's "copy from global" buttons.
    public void SeedPerKeyColorsFromGlobal() {
        for(int i = 0; i < SlotCount; i++) SeedPerKeyColorsFromGlobal(i);
    }

    // Seed a single slot's colours from the current shared values. Called when a
    // slot first opts in so it keeps the current look rather than snapping to the
    // array defaults.
    public void SeedPerKeyColorsFromGlobal(int slot) {
        if(slot < 0 || slot >= SlotCount) return;
        PerKeyBg[slot] = GetBg();
        PerKeyBgPressed[slot] = GetBgPressed();
        PerKeyOutline[slot] = GetOutline();
        PerKeyOutlinePressed[slot] = GetOutlinePressed();
        PerKeyText[slot] = GetText();
        PerKeyTextPressed[slot] = GetTextPressed();
        PerKeyRain[slot] = GetRain();
    }

    public void SeedPerKeyFontFromGlobal() {
        for(int i = 0; i < SlotCount; i++) SeedPerKeyFontFromGlobal(i);
    }

    public void SeedPerKeyFontFromGlobal(int slot) {
        if(slot < 0 || slot >= SlotCount) return;
        PerKeyKeyFont[slot] = KeyFontScale;
        PerKeyCounterFont[slot] = CounterFontScale;
    }

    // Copy one slot's per-key colours onto every slot and opt them all in, so a
    // "copy this key to all" action makes the whole viewer match the edited key.
    // (The shared-colour seeders above are the inverse — global onto the slots.)
    public void CopyPerKeyColorsToAll(int slot) {
        if(slot < 0 || slot >= SlotCount) return;
        Color bg = PerKeyBg[slot], bgP = PerKeyBgPressed[slot];
        Color ol = PerKeyOutline[slot], olP = PerKeyOutlinePressed[slot];
        Color tx = PerKeyText[slot], txP = PerKeyTextPressed[slot];
        Color rn = PerKeyRain[slot];
        for(int i = 0; i < SlotCount; i++) {
            PerKeyBg[i] = bg;
            PerKeyBgPressed[i] = bgP;
            PerKeyOutline[i] = ol;
            PerKeyOutlinePressed[i] = olP;
            PerKeyText[i] = tx;
            PerKeyTextPressed[i] = txP;
            PerKeyRain[i] = rn;
            PerKeyColorEnabled[i] = true;
            PerKeyColorInit[i] = true;
        }
    }

    private static float[] Filled(int n, float v) {
        float[] a = new float[n];
        for(int i = 0; i < n; i++) a[i] = v;
        return a;
    }

    private static bool[] Filled(int n, bool v) {
        bool[] a = new bool[n];
        for(int i = 0; i < n; i++) a[i] = v;
        return a;
    }

    private static Color[] FilledColor(int n, Color c) {
        Color[] a = new Color[n];
        for(int i = 0; i < n; i++) a[i] = c;
        return a;
    }

    public int GetCount(string key) =>
        key != null && Counts.TryGetValue(key, out int v) ? v : 0;

    public void SetCount(string key, int value) {
        if(!string.IsNullOrEmpty(key)) Counts[key] = value;
    }
}

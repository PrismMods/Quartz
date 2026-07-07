using HarmonyLib;
using Quartz.Core;
using Quartz.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Quartz.Features.InGameOverlay;

// Applies the mod's selected font (FontManager.Current) to three SPECIFIC,
// independently-toggleable pieces of A Dance of Fire and Ice's own native HUD
// text — nothing else. In place: no repositioning/resizing beyond what's
// needed to keep the wider mod font from overflowing its box; the game keeps
// driving position, animation and show/hide.
//
//   Song Title — scrHUDText.isTitle (the level title HUD)
//   Countdown  — scrCountdown (the pre-level "3, 2, 1, Go!" text)
//   Judgement  — scrHitTextMesh (the per-hit "Perfect!"/"Early!" popup —
//                DIFFERENT from Quartz's own JudgementOverlay, which shows
//                accumulated hit COUNTS, not this per-hit rating text)
//
// This replaces an earlier, much broader version of this idea that scanned the
// whole scene for arbitrary Text/TMP objects on a recurring timer and got
// pulled for hurting performance (see memory: quartz-gameoverlayfont-perf-history).
// This version never scans the scene: each category is discovered ONLY via the
// specific native call that fonts it (RDString.SetLocalizedFont for Song
// Title/Countdown, scrHitTextMesh.Show for Judgement — the same hook
// Features/Judgement/JudgementPopupHider.cs already patches for something
// else), plus a targeted one-object lookup when a toggle is flipped on
// mid-scene to catch whatever's already showing.
public static class InGameOverlayFont {
    // public: GameFontMirror (below, a separate top-level class) needs it too,
    // and the Overlay-tab settings pages pass it to RefreshSizeOnly.
    public enum Category { SongTitle, Countdown, Judgement }

    private sealed class Capture {
        public Category Cat;
        public TMP_Text Tmp;
        public TMP_FontAsset Original;
        public float OriginalSize;
        public TextWrappingModes OriginalWrap;
    }

    private static readonly Dictionary<int, Capture> tmpCaptures = [];
    private static bool hooked;

    private static bool TitleActive => MainCore.IsModEnabled && MainCore.Conf.FontSongTitle && FontManager.Current != null;
    private static bool CountdownActive => MainCore.IsModEnabled && MainCore.Conf.FontCountdown && FontManager.Current != null;
    private static bool JudgementActive => MainCore.IsModEnabled && MainCore.Conf.FontJudgement && FontManager.Current != null;

    public static void Initialize() {
        if(hooked) return;
        hooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static void Unhook() {
        if(hooked) {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            hooked = false;
        }
    }

    // A fresh scene may already contain the title/countdown objects (their own
    // Awake — and the SetLocalizedFont call inside it — already ran before we
    // can react). One targeted catch-up per category, not a scene-wide scan.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Async.MainThread.Enqueue(Refresh);
    }

    // Re-syncs all three categories to their current toggle/font state. Called
    // on scene load, mod enable, a toggle flip, and FontManager.OnFontChanged —
    // never on a timer.
    public static void Refresh() {
        RefreshTitle();
        RefreshCountdown();
        RefreshJudgement();
    }

    private static void RefreshTitle() {
        if(!TitleActive) { RestoreCategory(Category.SongTitle); return; }
        foreach(scrHUDText hud in UnityEngine.Object.FindObjectsByType<scrHUDText>(FindObjectsSortMode.None)) {
            if(!hud.isTitle) continue;
            ApplyToHudObject(hud.gameObject, Category.SongTitle);
        }
    }

    private static void RefreshCountdown() {
        if(!CountdownActive) { RestoreCategory(Category.Countdown); return; }
        foreach(scrCountdown cd in UnityEngine.Object.FindObjectsByType<scrCountdown>(FindObjectsSortMode.None)) {
            ApplyToHudObject(cd.gameObject, Category.Countdown);
        }
    }

    // Judgement has nothing persistent to catch up — popups are pooled and
    // re-shown per hit, so toggling on just means the NEXT Show() picks it up.
    // Toggling off restores whatever's currently swapped.
    private static void RefreshJudgement() {
        if(!JudgementActive) RestoreCategory(Category.Judgement);
    }

    // A HUD object's text may be native TMP or legacy Text depending on which
    // UI system this specific label uses — handle both.
    private static void ApplyToHudObject(GameObject go, Category cat) {
        var tmp = go.GetComponent<TMP_Text>();
        if(tmp != null) { OverrideTmp(tmp, cat); return; }
        var text = go.GetComponent<Text>();
        if(text != null) GameFontMirror.Ensure()?.Track(text, cat);
    }

    private static void RestoreCategory(Category cat) {
        GameFontMirror.Instance?.RestoreCategory(cat);

        List<int> dead = null;
        foreach(var kv in tmpCaptures) {
            if(kv.Value.Cat != cat) continue;
            if(kv.Value.Tmp != null) {
                kv.Value.Tmp.font = kv.Value.Original;
                kv.Value.Tmp.textWrappingMode = kv.Value.OriginalWrap;
                kv.Value.Tmp.fontSize = kv.Value.OriginalSize;
            }
            (dead ??= []).Add(kv.Key);
        }
        if(dead != null) foreach(int id in dead) tmpCaptures.Remove(id);
    }

    public static void RestoreAll() {
        GameFontMirror.DisposeInstance();
        foreach(Capture cap in tmpCaptures.Values) {
            if(cap.Tmp == null) continue;
            cap.Tmp.font = cap.Original;
            cap.Tmp.textWrappingMode = cap.OriginalWrap;
            cap.Tmp.fontSize = cap.OriginalSize;
        }
        tmpCaptures.Clear();
    }

    private static void OverrideTmp(TMP_Text tmp, Category cat) {
        TMP_FontAsset want = FontManager.Current;
        if(tmp == null || want == null) return;

        int id = tmp.GetInstanceID();
        if(!tmpCaptures.ContainsKey(id)) {
            float gameSize = tmp.fontSize;
            // Defer if the label's own size isn't resolved yet (still at a
            // prefab-default 0 — the localization hook can fire before a
            // freshly-created label's layout pass sets its real size). A later
            // Refresh (toggle flip / font change / next scene) retries.
            if(gameSize <= 0f) return;

            tmpCaptures[id] = new Capture {
                Cat = cat,
                Tmp = tmp,
                Original = tmp.font,
                OriginalSize = gameSize,
                OriginalWrap = tmp.textWrappingMode,
            };

            tmp.font = want;
            tmp.fontSharedMaterial = want.material;
            ApplySize(tmp, gameSize, cat);
        } else if(tmp.font != want) {
            tmp.font = want;
            tmp.fontSharedMaterial = want.material;
        }
    }

    // Single-line labels keep the game's size but shrink only when the wider
    // mod font overflows the box horizontally; the user's size multiplier
    // applies on top of that auto-fit.
    private static void ApplySize(TMP_Text tmp, float gameSize, Category cat) {
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        float boxW = tmp.rectTransform.rect.width;
        float wantW = tmp.GetPreferredValues(tmp.text).x;
        float fit = (boxW > 0f && wantW > boxW) ? gameSize * (boxW / wantW) * 0.98f : gameSize;
        tmp.fontSize = fit * SizeMultiplier(cat);
    }

    // Re-applies sizing (current multiplier) to every already-captured TMP
    // item in this category, from its stored ORIGINAL game size — no
    // re-scan, no re-capture. Cheap enough to call on every slider drag tick.
    public static void RefreshSizeOnly(Category cat) {
        foreach(Capture cap in tmpCaptures.Values) {
            if(cap.Cat == cat && cap.Tmp != null) ApplySize(cap.Tmp, cap.OriginalSize, cat);
        }
        // Twin-mirrored items (GameFontMirror.Apply) recompute size from the
        // live source every frame and already read the multiplier, so they
        // need no explicit push here.
    }

    internal static float SizeMultiplier(Category cat) => cat switch {
        Category.SongTitle => MainCore.Conf.FontSongTitleSize,
        Category.Countdown => MainCore.Conf.FontCountdownSize,
        Category.Judgement => MainCore.Conf.FontJudgementSize,
        _ => 1f,
    };

    [HarmonyPatch(typeof(RDString), "SetLocalizedFont", new[] { typeof(TMP_Text) })]
    private static class TmpFontPatch {
        private static void Postfix(TMP_Text text) {
            try {
                if(text == null) return;
                if(TitleActive && text.GetComponent<scrHUDText>() is { isTitle: true }) OverrideTmp(text, Category.SongTitle);
            } catch(Exception e) {
                MainCore.Log.Wrn($"[InGameOverlayFont] TmpFontPatch: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(RDString), "SetLocalizedFont", new[] { typeof(Text) })]
    private static class TextFontPatch {
        private static void Postfix(Text text) {
            try {
                if(text == null) return;
                if(TitleActive && text.GetComponent<scrHUDText>() is { isTitle: true }) {
                    GameFontMirror.Ensure()?.Track(text, Category.SongTitle);
                } else if(CountdownActive && text.GetComponent<scrCountdown>() != null) {
                    GameFontMirror.Ensure()?.Track(text, Category.Countdown);
                }
            } catch(Exception e) {
                MainCore.Log.Wrn($"[InGameOverlayFont] TextFontPatch: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(scrHitTextMesh), "Show")]
    private static class JudgementFontPatch {
        private static void Postfix(scrHitTextMesh __instance) {
            if(!JudgementActive) return;
            try {
                if(__instance.text != null) OverrideTmp(__instance.text, Category.Judgement);
            } catch(Exception e) {
                MainCore.Log.Wrn($"[InGameOverlayFont] JudgementFontPatch: {e}");
            }
        }
    }
}

// Mirrors a tracked legacy UI.Text onto an overlaid TextMeshProUGUI twin in the
// mod font, hiding the original's glyphs (CanvasRenderer alpha — independent
// of the Text's own colour/enabled state, so the game keeps full control).
// Lives on the mod root so it survives scene loads. Only ever holds the
// handful of pairs InGameOverlayFont explicitly Tracks (title/countdown) —
// never a scene-wide scan, so this stays a small, fixed-size list.
internal sealed class GameFontMirror : MonoBehaviour {
    private sealed class Pair {
        public InGameOverlayFont.Category Cat;
        public Text Source;
        public TextMeshProUGUI Twin;
        public string LastRaw;
    }

    private const string TwinName = "QuartzInGameFontTwin";

    private static GameFontMirror instance;
    private static readonly HashSet<int> twinIds = [];
    private readonly List<Pair> pairs = [];
    private readonly HashSet<int> trackedSources = [];

    public static GameFontMirror Instance => instance;

    public static GameFontMirror Ensure() {
        if(instance == null && MainCore.Root != null) instance = MainCore.Root.AddComponent<GameFontMirror>();
        return instance;
    }

    public static void DisposeInstance() {
        if(instance != null) {
            instance.Clear();
            Destroy(instance);
            instance = null;
        }
        twinIds.Clear();
    }

    public void Track(Text source, InGameOverlayFont.Category cat) {
        if(source == null || !trackedSources.Add(source.GetInstanceID())) return;

        var twinGo = new GameObject(TwinName);
        twinGo.transform.SetParent(source.transform, false);

        var rt = twinGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        var twin = twinGo.AddComponent<TextMeshProUGUI>();
        twin.font = FontManager.Current;
        twin.raycastTarget = false;
        twinIds.Add(twin.GetInstanceID());

        var pair = new Pair { Cat = cat, Source = source, Twin = twin };
        pairs.Add(pair);
        Apply(pair);
    }

    // Un-hides and drops every twin tagged with this category; leaves the
    // other two untouched.
    public void RestoreCategory(InGameOverlayFont.Category cat) {
        for(int i = pairs.Count - 1; i >= 0; i--) {
            Pair pair = pairs[i];
            if(pair.Cat != cat) continue;
            if(pair.Twin != null) {
                twinIds.Remove(pair.Twin.GetInstanceID());
                Destroy(pair.Twin.gameObject);
            }
            if(pair.Source != null) {
                trackedSources.Remove(pair.Source.GetInstanceID());
                pair.Source.canvasRenderer.SetAlpha(1f);
            }
            pairs.RemoveAt(i);
        }
    }

    // The per-frame twin sync runs on the canvas pre-render, not Update:
    // willRenderCanvases is the last main-thread callback before the UI draws,
    // so SetAlpha(0) on the source there takes effect the same frame with no
    // rebuild. This is the only per-frame work this class does — discovery
    // happens exclusively through InGameOverlayFont's event hooks.
    private int lastSyncFrame = -1;

    private void OnEnable() => Canvas.willRenderCanvases += SyncPairs;
    private void OnDisable() => Canvas.willRenderCanvases -= SyncPairs;

    private void SyncPairs() {
        int frame = Time.frameCount;
        if(lastSyncFrame == frame) return;
        lastSyncFrame = frame;

        for(int i = pairs.Count - 1; i >= 0; i--) {
            Pair pair = pairs[i];
            if(pair.Source == null || pair.Twin == null) {
                if(pair.Twin != null) {
                    twinIds.Remove(pair.Twin.GetInstanceID());
                    Destroy(pair.Twin.gameObject);
                }
                if(pair.Source != null) trackedSources.Remove(pair.Source.GetInstanceID());
                pairs.RemoveAt(i);
                continue;
            }
            Apply(pair);
        }
    }

    private static void Apply(Pair pair) {
        Text source = pair.Source;
        TextMeshProUGUI twin = pair.Twin;

        TMP_FontAsset want = FontManager.Current;
        if(want != null && twin.font != want) twin.font = want;

        // TMP's text setter does not early-out on an equal string — it
        // re-tessellates the whole mesh — so guard on the raw source string.
        if(pair.LastRaw != source.text) {
            pair.LastRaw = source.text;
            twin.text = source.text;
        }
        if(twin.color != source.color) twin.color = source.color;
        if(twin.richText != source.supportRichText) twin.richText = source.supportRichText;

        // TextMeshProUGUI defaults to TopLeft — without mirroring the source's
        // own alignment/style, a center-anchored label (the countdown number,
        // dead center of the screen) renders the twin jammed into a corner
        // instead, which reads as "the text is just gone".
        FontStyles style = MapStyle(source.fontStyle);
        if(twin.fontStyle != style) twin.fontStyle = style;
        TextAlignmentOptions alignment = MapAlignment(source.alignment);
        if(twin.alignment != alignment) twin.alignment = alignment;

        bool wrap = source.horizontalOverflow == HorizontalWrapMode.Wrap;
        TextWrappingModes wrapMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        if(twin.textWrappingMode != wrapMode) twin.textWrappingMode = wrapMode;
        if(twin.overflowMode != TextOverflowModes.Overflow) twin.overflowMode = TextOverflowModes.Overflow;

        float mult = InGameOverlayFont.SizeMultiplier(pair.Cat);
        if(source.resizeTextForBestFit) {
            float maxPx = source.resizeTextMaxSize > 0 ? source.resizeTextMaxSize : source.fontSize;
            if(!twin.enableAutoSizing) twin.enableAutoSizing = true;
            if(twin.fontSizeMin != 1f) twin.fontSizeMin = 1f;
            float max = Mathf.Max(1f, maxPx) * mult;
            if(twin.fontSizeMax != max) twin.fontSizeMax = max;
        } else {
            if(twin.enableAutoSizing) twin.enableAutoSizing = false;
            float boxW = source.rectTransform.rect.width;
            float size = source.fontSize;
            if(boxW > 0f) {
                float wantW = twin.GetPreferredValues(twin.text).x;
                if(wantW > boxW) size = source.fontSize * (boxW / wantW) * 0.98f;
            }
            size *= mult;
            if(twin.fontSize != size) twin.fontSize = size;
        }
        if(twin.enabled != source.enabled) twin.enabled = source.enabled;

        // Mirrors BOTH fade mechanisms the game might drive this label with: a
        // plain Color.a change (the twin.color copy above) and a
        // CrossFadeAlpha-style CanvasRenderer multiplier (here).
        float srcAlpha = source.canvasRenderer.GetAlpha();
        if(twin.canvasRenderer.GetAlpha() != srcAlpha) twin.canvasRenderer.SetAlpha(srcAlpha);
        source.canvasRenderer.SetAlpha(0f);
    }

    private static FontStyles MapStyle(FontStyle style) => style switch {
        FontStyle.Bold => FontStyles.Bold,
        FontStyle.Italic => FontStyles.Italic,
        FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
        _ => FontStyles.Normal,
    };

    private static TextAlignmentOptions MapAlignment(TextAnchor anchor) => anchor switch {
        TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
        TextAnchor.UpperCenter => TextAlignmentOptions.Top,
        TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
        TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
        TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
        TextAnchor.MiddleRight => TextAlignmentOptions.Right,
        TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
        TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
        TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
        _ => TextAlignmentOptions.Center,
    };

    private void Clear() {
        foreach(Pair pair in pairs) {
            if(pair.Twin != null) Destroy(pair.Twin.gameObject);
            if(pair.Source != null) pair.Source.canvasRenderer.SetAlpha(1f);
        }
        pairs.Clear();
        trackedSources.Clear();
    }
}

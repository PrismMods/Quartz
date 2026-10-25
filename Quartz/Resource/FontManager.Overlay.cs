using Quartz.Core;
using TMPro;
using UnityEngine;
namespace Quartz.Resource;
// Per-overlay font picks, and the game's own Korean font offered as a choice.
public static partial class FontManager {
    public const string GameFontName = "godoMaum (ADOFAI)";
    // Font list entry an overlay uses to drop its own pick and follow the global font.
    public const string FollowGlobal = "quartz-overlay-font-global";
    private static TMP_FontAsset gameFont;
    private static bool gameFontOwnsAtlas;
    // The overlay's own font when it has one, otherwise the global font. Cheap enough for per-frame checks.
    public static TMP_FontAsset For(string overlayKey) {
        string name = OverlayFontName(overlayKey);
        return name == null ? Current : Resolve(name) ?? Current;
    }
    // null when the overlay follows the global font.
    public static string OverlayFontName(string overlayKey) =>
        overlayKey != null && MainCore.Conf != null
        && MainCore.Conf.OverlayFonts.TryGetValue(overlayKey, out string name) && !string.IsNullOrEmpty(name)
            ? name : null;
    public static void SetOverlayFont(string overlayKey, string name) {
        if(string.IsNullOrEmpty(overlayKey) || MainCore.Conf == null) return;
        if(string.IsNullOrEmpty(name) || name == FollowGlobal) MainCore.Conf.OverlayFonts.Remove(overlayKey);
        else {
            TMP_FontAsset asset = Resolve(name);
            if(asset != null) ApplyAdjust(asset, name);
            MainCore.Conf.OverlayFonts[overlayKey] = name;
        }
        MainCore.ConfMgr.RequestSave();
        ApplyToAll();
        OnFontChanged?.Invoke();
    }
    private static TMP_FontAsset FontFor(TMP_Text text) {
        OverlayFontScope scope = text.GetComponentInParent<OverlayFontScope>(true);
        return scope == null ? Current : For(scope.Key);
    }
    // A separate asset: changing the game's own one would also restyle the game's Korean UI text.
    private static TMP_FontAsset GameFont() {
        if(gameFont != null) return gameFont;
        TMP_FontAsset src = ADOBase.gc?.koreanFontTMPro;
        if(src == null) return null;
        bool primed = EnsureTmpSettings();
        try {
            if(src.sourceFontFile != null) {
                gameFont = TMP_FontAsset.CreateFontAsset(src.sourceFontFile);
                gameFont.isMultiAtlasTexturesEnabled = true;
                gameFontOwnsAtlas = true;
            } else {
                // No source font to rasterise from: share the baked atlas, frozen so nothing writes into the game's texture.
                gameFont = UnityEngine.Object.Instantiate(src);
                gameFont.atlasPopulationMode = AtlasPopulationMode.Static;
                gameFontOwnsAtlas = false;
            }
            gameFont.name = GameFontName;
            AttachFallbacks(gameFont);
            ApplyAdjust(gameFont, GameFontName);
            return gameFont;
        } catch(Exception e) {
            MainCore.Log.Wrn($"[FontManager] game font failed: {e.Message}");
            gameFont = null;
            return null;
        } finally {
            if(primed) ReleaseTmpSettings();
        }
    }
    private static void DisposeGameFont() {
        if(gameFont == null) return;
        baseFaces.Remove(gameFont);
        if(gameFontOwnsAtlas) DestroyFontAsset(gameFont);
        else UnityEngine.Object.Destroy(gameFont);
        gameFont = null;
    }
}

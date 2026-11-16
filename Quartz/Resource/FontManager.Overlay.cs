using Quartz.Core;
using TMPro;
using UnityEngine;
namespace Quartz.Resource;
// Per-overlay font picks, and the game's own Korean font offered as a choice.
public static partial class FontManager {
    public const string GameFontName = "godoMaum (ADOFAI)";
    // Font list entry an overlay uses to drop its own pick and follow the global font.
    public const string FollowGlobal = "quartz-overlay-font-global";
    // Font list entry for the game's own UI face, which follows the game's language.
    public const string GameDefault = "quartz-overlay-font-game";
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
        gameFont = CopyGameFace(ADOBase.gc?.koreanFontTMPro, GameFontName, out gameFontOwnsAtlas);
        return gameFont;
    }
    // The game's UI face for its current language (RDString.fontData); one copy per face, so a language switch
    // makes a new copy and leaves any text still on the old one intact. All are freed in Dispose.
    private static readonly Dictionary<TMP_FontAsset, (TMP_FontAsset Copy, bool OwnsAtlas)> gameDefaults = [];
    private static TMP_FontAsset gameDefaultFont;
    internal static TMP_FontAsset GameDefaultSource => RDString.fontData.fontTMP;
    // The face's family name for menus, e.g. "Game default (NanumSquare)".
    public static string GameDefaultFace {
        get {
            TMP_FontAsset src = GameDefaultSource;
            if(src == null) return null;
            string family = src.faceInfo.familyName;
            return string.IsNullOrEmpty(family) ? src.name : family;
        }
    }
    private static TMP_FontAsset GameDefaultFont() {
        TMP_FontAsset src = GameDefaultSource;
        if(src == null) return null;
        if(!gameDefaults.TryGetValue(src, out var made)) {
            TMP_FontAsset copy = CopyGameFace(src, GameDefault, out bool owns);
            if(copy == null) return null;
            gameDefaults[src] = made = (copy, owns);
        }
        return gameDefaultFont = made.Copy;
    }
    private static TMP_FontAsset CopyGameFace(TMP_FontAsset src, string name, out bool ownsAtlas) {
        ownsAtlas = false;
        if(src == null) return null;
        bool primed = EnsureTmpSettings();
        TMP_FontAsset copy = null;
        try {
            if(src.sourceFontFile != null) {
                copy = TMP_FontAsset.CreateFontAsset(src.sourceFontFile);
                copy.isMultiAtlasTexturesEnabled = true;
                ownsAtlas = true;
            } else {
                // No source font to rasterise from: share the baked atlas, frozen so nothing writes into the game's texture.
                copy = UnityEngine.Object.Instantiate(src);
                copy.atlasPopulationMode = AtlasPopulationMode.Static;
            }
            copy.name = name == GameDefault ? src.name : name;
            AttachFallbacks(copy);
            ApplyAdjust(copy, name);
            return copy;
        } catch(Exception e) {
            MainCore.Log.Wrn($"[FontManager] game font '{name}' failed: {e.Message}");
            return null;
        } finally {
            if(primed) ReleaseTmpSettings();
        }
    }
    private static void DisposeGameFont() {
        if(gameFont != null) DisposeCopy(gameFont, gameFontOwnsAtlas);
        gameFont = null;
        foreach(var made in gameDefaults.Values) DisposeCopy(made.Copy, made.OwnsAtlas);
        gameDefaults.Clear();
        gameDefaultFont = null;
    }
    private static void DisposeCopy(TMP_FontAsset copy, bool ownsAtlas) {
        if(copy == null) return;
        baseFaces.Remove(copy);
        if(ownsAtlas) DestroyFontAsset(copy);
        else UnityEngine.Object.Destroy(copy);
    }
}

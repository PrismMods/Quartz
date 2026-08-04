using Quartz.Core;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;
using TMPro;
using Quartz.Compat.Game;
namespace Quartz.Resource;
public enum Asset {
    SUIT_Regular = 0,
    SUIT_Medium = 1,
    QuartzLogo = 2,
    OV5LogoOutline256 = 3,
    Circle256 = 4,
    CircleHalf256 = 5,
    X128 = 6,
    Monitor128 = 7,
    Gear128 = 8,
    Image128 = 9,
    Text128 = 10,
    Book128 = 11,
    Star128 = 12,
    ToggleCircle128 = 13,
    CircleOutline256 = 14,
    Triangle128 = 15,
    Power128 = 16,
    MagnifyingGlass128 = 17,
    Gamepad128 = 18,
    Wrench128 = 19,
    AdjustmentsHorizontal128 = 20,
    Users128 = 21,
    ClockRewind128 = 22,
    Trash128 = 23,
    OttoAuto = 24,
    QuantumQ = 25,
    TufLogo = 26,
    Move128 = 27,
    Eraser128 = 28,
    Layer128 = 29,
    Primary128 = 30,
    Broom128 = 31,
    Grid128 = 32,
    Plus128 = 33,
    PlusBold128 = 34,
    Minus128 = 35,
    Reset128 = 36,
    TurnArrow128 = 37,
    EyeOpen128 = 38,
    EyeClosed128 = 39,
    Palette128 = 40,
    Note128 = 41,
    ChevronDown128 = 42,
    Folder128 = 43,
    QuestionMarkCircle128 = 44,
    Tabub = 45,
    QuizLogo = 46,
}
public sealed class ResourceManager(Assembly assembly, string resourcePath) : IDisposable {
    private readonly Dictionary<string, object> cache = [];
    private readonly List<Font> sourceFonts = [];
    public byte[] Load(string path) {
        if(string.IsNullOrWhiteSpace(path)) return null;
        try {
            using Stream stream = assembly.GetManifestResourceStream(resourcePath + path);
            if(stream == null) return null;
            if(stream.Length <= 0) return [];
            byte[] data = new byte[stream.Length];
            int offset = 0;
            while(offset < data.Length) {
                int read = stream.Read(data, offset, data.Length - offset);
                if(read <= 0) break;
                offset += read;
            }
            return offset == data.Length ? data : null;
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    public Texture2D LoadTexture(string path, FilterMode filter = FilterMode.Trilinear) {
        if(cache.TryGetValue(path, out object cached)) return cached as Texture2D;
        byte[] data = Load(path);
        if(data == null || data.Length == 0) return null;
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, true, true);
        if(!texture.LoadImage(data)) {
            Object.Destroy(texture);
            return null;
        }
        texture.filterMode = filter;
        texture.anisoLevel = 4;
        texture.mipMapBias = -0.7f;
        cache[path] = texture;
        return texture;
    }
    public TMP_FontAsset LoadFont(string path, string tempPath) {
        if(cache.TryGetValue(path, out object cached)) return cached as TMP_FontAsset;
        byte[] data = Load(path);
        if(data == null) return null;
        string directory = Path.GetDirectoryName(tempPath);
        if(!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        if(!File.Exists(tempPath) || new FileInfo(tempPath).Length != data.Length) File.WriteAllBytes(tempPath, data);
        Font font = new(tempPath);
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
        cache[path] = asset;
        sourceFonts.Add(font);
        return asset;
    }
    public T Get<T>(Asset asset) where T : class {
        if(!assetMap.TryGetValue(asset, out string path)) return null;
        return GetInternal<T>(path, asset.ToString());
    }
    public T Get<T>(string path) where T : class {
        if(string.IsNullOrWhiteSpace(path)) return null;
        string fileName = Path.GetFileNameWithoutExtension(path);
        return GetInternal<T>(path, fileName);
    }
    public TMP_FontAsset GetFont(string path, string customTempPath) => LoadFont(path, customTempPath);
    private T GetInternal<T>(string path, string assetNameForFont) where T : class {
        object result = null;
        if(typeof(T) == typeof(Texture2D)) {
            result = LoadTexture(path);
        } else if(typeof(T) == typeof(TMP_FontAsset)) {
            string tempPath = Path.Combine(MainCore.Paths.TempPath, assetNameForFont + ".otf");
            result = LoadFont(path, tempPath);
        }
        return result as T;
    }
    public void Dispose() {
        foreach(object item in cache.Values) {
            switch(item) {
                case Texture2D texture:
                    Object.Destroy(texture);
                    break;
                case TMP_FontAsset font:
                    Material fontMaterial = GameApi.FontMaterial(font);
                    if(fontMaterial != null) Object.Destroy(fontMaterial);
                    Texture2D[] atlases = font.atlasTextures;
                    if(atlases != null)
                        foreach(Texture2D tex in atlases) if(tex != null) Object.Destroy(tex);
                    Object.Destroy(font);
                    break;
            }
        }
        foreach(Font font in sourceFonts) if(font != null) Object.Destroy(font);
        sourceFonts.Clear();
        cache.Clear();
    }
    private readonly Dictionary<Asset, string> assetMap = new() {
        [Asset.SUIT_Regular] = "Font.SUIT-Regular.otf",
        [Asset.SUIT_Medium] = "Font.SUIT-Medium.otf",
        [Asset.QuartzLogo] = "Image.QuartzLogo.png",
        [Asset.OV5LogoOutline256] = "Image.OV5LogoOutline256.png",
        [Asset.Circle256] = "Image.Circle256.png",
        [Asset.CircleHalf256] = "Image.CircleHalf256.png",
        [Asset.X128] = "Image.X128.png",
        [Asset.Monitor128] = "Image.Monitor128.png",
        [Asset.Gear128] = "Image.Gear128.png",
        [Asset.Image128] = "Image.Image128.png",
        [Asset.Text128] = "Image.Text128.png",
        [Asset.Book128] = "Image.Book128.png",
        [Asset.Star128] = "Image.Star128.png",
        [Asset.ToggleCircle128] = "Image.ToggleCircle128.png",
        [Asset.CircleOutline256] = "Image.CircleOutline256.png",
        [Asset.Triangle128] = "Image.Triangle128.png",
        [Asset.Power128] = "Image.Power128.png",
        [Asset.MagnifyingGlass128] = "Image.MagnifyingGlass128.png",
        [Asset.Gamepad128] = "Image.Gamepad128.png",
        [Asset.Wrench128] = "Image.Wrench128.png",
        [Asset.AdjustmentsHorizontal128] = "Image.AdjustmentsHorizontal128.png",
        [Asset.Users128] = "Image.Users128.png",
        [Asset.ClockRewind128] = "Image.ClockRewind128.png",
        [Asset.Trash128] = "Image.Trash128.png",
        [Asset.OttoAuto] = "Image.OttoAuto.png",
        [Asset.QuantumQ] = "Image.QuantumQ.png",
        [Asset.TufLogo] = "Image.TufLogo.png",
        [Asset.Move128] = "Image.Move128.png",
        [Asset.Eraser128] = "Image.Eraser128.png",
        [Asset.Layer128] = "Image.Layer128.png",
        [Asset.Primary128] = "Image.Primary128.png",
        [Asset.Broom128] = "Image.Broom128.png",
        [Asset.Grid128] = "Image.Grid128.png",
        [Asset.Plus128] = "Image.Plus128.png",
        [Asset.PlusBold128] = "Image.PlusBold128.png",
        [Asset.Minus128] = "Image.Minus128.png",
        [Asset.Reset128] = "Image.Reset128.png",
        [Asset.TurnArrow128] = "Image.TurnArrow128.png",
        [Asset.EyeOpen128] = "Image.EyeOpen128.png",
        [Asset.EyeClosed128] = "Image.EyeClosed128.png",
        [Asset.Palette128] = "Image.Palette128.png",
        [Asset.Note128] = "Image.Note128.png",
        [Asset.ChevronDown128] = "Image.ChevronDown128.png",
        [Asset.Folder128] = "Image.Folder128.png",
        [Asset.QuestionMarkCircle128] = "Image.QuestionMarkCircle128.png",
        [Asset.Tabub] = "Image.Tabub.png",
        [Asset.QuizLogo] = "Image.QuizLogo.png"
    };
}

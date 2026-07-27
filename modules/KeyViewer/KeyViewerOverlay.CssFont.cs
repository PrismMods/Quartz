using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Quartz.Core;
using Quartz.Resource;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static readonly object cssFontLock = new();
    private static void EnsureFontFaces(KeyViewerStylesheet sheet) {
        foreach(CssFontFace face in sheet.FontFaces) {
            try {
                if(cssFonts.ContainsKey(face.Family)) continue;
                lock(cssFontLock) {
                    if(cssFontPending.Contains(face.Family)) continue;
                }
                string path = CachedFontPath(face);
                if(path != null && File.Exists(path)) {
                    BuildFont(face.Family, path);
                } else {
                    StartFontDownload(face);
                }
            } catch(Exception ex) {
                MainCore.Log.Msg($"[KeyViewer] CSS @font-face '{face.Family}' failed: {ex.Message}");
            }
        }
    }
    private static TMP_FontAsset ResolveFont(string family) {
        if(string.IsNullOrEmpty(family)) return null;
        if(cssFonts.TryGetValue(family, out TMP_FontAsset asset)) return asset;
        foreach(string name in FontManager.GetAvailableFonts()) {
            if(string.Equals(name, family, StringComparison.OrdinalIgnoreCase)) {
                TMP_FontAsset f = FontManager.GetFont(name);
                cssFonts[family] = f;
                return f;
            }
        }
        return null;
    }
    private static string FontCacheDir() {
        string dir = Path.Combine(MainCore.Paths.RootPath, "CssFonts");
        Directory.CreateDirectory(dir);
        return dir;
    }
    private static string CachedFontPath(CssFontFace face) {
        string url = PickFontUrl(face);
        if(url == null) return null;
        string ext = Path.GetExtension(new Uri(url).AbsolutePath);
        if(ext != ".ttf" && ext != ".otf" && ext != ".ttc") ext = ".ttf";
        return Path.Combine(FontCacheDir(), Hash(face.Family + "|" + url) + ext);
    }
    private static string PickFontUrl(CssFontFace face) {
        foreach(string s in face.Srcs) {
            string e = s.ToLowerInvariant();
            if(e.EndsWith(".ttf") || e.EndsWith(".otf") || e.EndsWith(".ttc")) return s;
        }
        return face.Srcs.Count > 0 ? SwapToTtf(face.Srcs[0]) : null;
    }
    private static string SwapToTtf(string url) {
        int dot = url.LastIndexOf('.');
        return dot > 0 ? url.Substring(0, dot) + ".ttf" : url;
    }
    private static void BuildFont(string family, string path) {
        try {
            Font font = new(path);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
            asset.isMultiAtlasTexturesEnabled = true;
            cssFonts[family] = asset;
        } catch(Exception ex) {
            MainCore.Log.Msg($"[KeyViewer] CSS font '{family}' build failed: {ex.Message}");
            cssFonts[family] = null;
        }
    }
    private static void StartFontDownload(CssFontFace face) {
        string url = PickFontUrl(face);
        string path = CachedFontPath(face);
        if(url == null || path == null) return;
        lock(cssFontLock) cssFontPending.Add(face.Family);
        StartCssDownload(url, path, $"CSS font download failed ({face.Family})",
            "QuartzCssFont", cssFontLock, cssFontPending, face.Family);
    }
    private static void StartCssDownload(string url, string path, string failWhat,
        string threadName, object gate, HashSet<string> pending, string pendingKey) {
        var thread = new Thread(() => {
            try {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using var client = new WebClient();
                File.WriteAllBytes(path, client.DownloadData(url));
                cssDownloadArrived = true;
            } catch(Exception ex) {
                MainCore.Log.Msg($"[KeyViewer] {failWhat}: {ex.Message}");
            } finally {
                lock(gate) pending.Remove(pendingKey);
            }
        }) { IsBackground = true, Name = threadName };
        thread.Start();
    }
    private static string Hash(string s) {
        using var md5 = MD5.Create();
        byte[] h = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
        var sb = new StringBuilder(h.Length * 2);
        foreach(byte b in h) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

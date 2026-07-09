using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
namespace Quartz.UI.Utility;
public enum ResizeCursorShape {
    Horizontal, 
    Vertical,   
    DiagNWSE,   
    DiagNESW,   
}
public static class NativeCursor {
    private static readonly bool isWindows =
        Application.platform == RuntimePlatform.WindowsPlayer
        || Application.platform == RuntimePlatform.WindowsEditor;
    private static readonly bool isMac =
        Application.platform == RuntimePlatform.OSXPlayer
        || Application.platform == RuntimePlatform.OSXEditor;
    public static void Apply(ResizeCursorShape shape) {
        try {
            if(isWindows) ApplyWindows(shape);
            else if(isMac) ApplyMac(shape);
            else ApplyTexture(shape);
        } catch {
            ApplyTexture(shape);
        }
    }
    public static void Reset() {
        try {
            if(isWindows) {
                SetCursor(LoadWinCursor(IDC_ARROW));
                return;
            }
            if(isMac) {
                MacSet(NSCursorByName("arrowCursor"));
                return;
            }
        } catch {
        }
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
    private const int IDC_ARROW = 32512;
    private const int IDC_SIZENWSE = 32642; 
    private const int IDC_SIZENESW = 32643; 
    private const int IDC_SIZEWE = 32644;   
    private const int IDC_SIZENS = 32645;   
    [DllImport("user32.dll", EntryPoint = "LoadCursorW")]
    private static extern IntPtr LoadCursorW(IntPtr hInstance, IntPtr lpCursorName);
    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr hCursor);
    private static IntPtr LoadWinCursor(int id) => LoadCursorW(IntPtr.Zero, (IntPtr)id);
    private static void ApplyWindows(ResizeCursorShape shape) {
        int id = shape switch {
            ResizeCursorShape.Horizontal => IDC_SIZEWE,
            ResizeCursorShape.Vertical => IDC_SIZENS,
            ResizeCursorShape.DiagNWSE => IDC_SIZENWSE,
            _ => IDC_SIZENESW,
        };
        SetCursor(LoadWinCursor(id));
    }
    private const string LIBOBJC = "/usr/lib/libobjc.A.dylib";
    [DllImport(LIBOBJC, EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);
    [DllImport(LIBOBJC, EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);
    [DllImport(LIBOBJC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);
    [DllImport(LIBOBJC, EntryPoint = "objc_msgSend")]
    private static extern byte objc_msgSend_bool(IntPtr receiver, IntPtr selector, IntPtr arg);
    private static IntPtr nsCursorClass;
    private static IntPtr respondsSel;
    private static IntPtr NSCursorByName(string selName) {
        if(nsCursorClass == IntPtr.Zero) nsCursorClass = objc_getClass("NSCursor");
        if(nsCursorClass == IntPtr.Zero) return IntPtr.Zero;
        if(respondsSel == IntPtr.Zero) respondsSel = sel_registerName("respondsToSelector:");
        IntPtr sel = sel_registerName(selName);
        if(objc_msgSend_bool(nsCursorClass, respondsSel, sel) == 0) return IntPtr.Zero;
        return objc_msgSend(nsCursorClass, sel);
    }
    private static void MacSet(IntPtr cursor) {
        if(cursor != IntPtr.Zero) objc_msgSend(cursor, sel_registerName("set"));
    }
    private static void ApplyMac(ResizeCursorShape shape) {
        IntPtr cursor = shape switch {
            ResizeCursorShape.Horizontal => NSCursorByName("resizeLeftRightCursor"),
            ResizeCursorShape.Vertical => NSCursorByName("resizeUpDownCursor"),
            ResizeCursorShape.DiagNWSE => Fallback(
                NSCursorByName("_windowResizeNorthWestSouthEastCursor"),
                "resizeLeftRightCursor"),
            _ => Fallback(
                NSCursorByName("_windowResizeNorthEastSouthWestCursor"),
                "resizeLeftRightCursor"),
        };
        MacSet(cursor);
    }
    private static IntPtr Fallback(IntPtr primary, string fallbackSel) =>
        primary != IntPtr.Zero ? primary : NSCursorByName(fallbackSel);
    private static readonly Dictionary<int, Texture2D> texCache = new();
    private static int Angle(ResizeCursorShape shape) => shape switch {
        ResizeCursorShape.Horizontal => 0,
        ResizeCursorShape.Vertical => 90,
        ResizeCursorShape.DiagNESW => 45,
        _ => 135, 
    };
    private static void ApplyTexture(ResizeCursorShape shape) {
        Texture2D tex = GetTexture(Angle(shape));
        if(tex != null) Cursor.SetCursor(tex, new Vector2(tex.width * 0.5f, tex.height * 0.5f), CursorMode.Auto);
    }
    private static Texture2D GetTexture(int angleDeg) {
        if(texCache.TryGetValue(angleDeg, out Texture2D cached)) return cached;
        const int size = 32;
        const float c = (size - 1) * 0.5f;
        float rad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        Texture2D tex = new(size, size, TextureFormat.RGBA32, false);
        Color clear = new(0f, 0f, 0f, 0f);
        for(int y = 0; y < size; y++) {
            for(int x = 0; x < size; x++) {
                float px = x - c;
                float py = y - c;
                float u = (px * cos) + (py * sin);
                float v = (-px * sin) + (py * cos);
                Color col = clear;
                if(IsArrow(u, v, 14f, 6f, 5f, 2.6f)) col = Color.black;
                if(IsArrow(u, v, 13f, 5f, 4f, 1.5f)) col = Color.white;
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply(false);
        tex.filterMode = FilterMode.Bilinear;
        texCache[angleDeg] = tex;
        return tex;
    }
    private static bool IsArrow(float u, float v, float ltip, float hh, float headHalf, float t) {
        float au = Mathf.Abs(u);
        float shaft = ltip - hh;
        if(au <= shaft) return Mathf.Abs(v) <= t;
        if(au <= ltip) return Mathf.Abs(v) <= headHalf * (ltip - au) / hh;
        return false;
    }
}

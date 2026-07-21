using UnityEngine;
namespace Quartz.Features.KeyViewer.Layout;
internal static partial class KvPresets {
    private const float Col108 = 56f;
    private const float Gap108 = 6f;
    private const float StatH108 = 30f;
    private const float TopRow108 = 580f;
    private const float RightCol108 = 19f;
    private const float RightShift108 = 4f * Col108;
    private static readonly (KeyCode Key, float Cx, float Jy, float Wu)[] Full108Slots = [
        (KeyCode.Escape, 0f, 580f, 1f),
        (KeyCode.F1, 2f, 580f, 1f), (KeyCode.F2, 3f, 580f, 1f), (KeyCode.F3, 4f, 580f, 1f),
        (KeyCode.F4, 5f, 580f, 1f), (KeyCode.F5, 6.5f, 580f, 1f), (KeyCode.F6, 7.5f, 580f, 1f),
        (KeyCode.F7, 8.5f, 580f, 1f), (KeyCode.F8, 9.5f, 580f, 1f), (KeyCode.F9, 11f, 580f, 1f),
        (KeyCode.F10, 12f, 580f, 1f), (KeyCode.F11, 13f, 580f, 1f), (KeyCode.F12, 14f, 580f, 1f),
        (KeyCode.Print, 15f, 580f, 1f), (KeyCode.ScrollLock, 16f, 580f, 1f),
        (KeyCode.Pause, 17f, 580f, 1f),
        (KeyCode.BackQuote, 0f, 524f, 1f), (KeyCode.Alpha1, 1f, 524f, 1f), (KeyCode.Alpha2, 2f, 524f, 1f),
        (KeyCode.Alpha3, 3f, 524f, 1f), (KeyCode.Alpha4, 4f, 524f, 1f), (KeyCode.Alpha5, 5f, 524f, 1f),
        (KeyCode.Alpha6, 6f, 524f, 1f), (KeyCode.Alpha7, 7f, 524f, 1f), (KeyCode.Alpha8, 8f, 524f, 1f),
        (KeyCode.Alpha9, 9f, 524f, 1f), (KeyCode.Alpha0, 10f, 524f, 1f), (KeyCode.Minus, 11f, 524f, 1f),
        (KeyCode.Equals, 12f, 524f, 1f), (KeyCode.Backspace, 13f, 524f, 2f),
        (KeyCode.Tab, 0f, 468f, 1.5f), (KeyCode.Q, 1.5f, 468f, 1f), (KeyCode.W, 2.5f, 468f, 1f),
        (KeyCode.E, 3.5f, 468f, 1f), (KeyCode.R, 4.5f, 468f, 1f), (KeyCode.T, 5.5f, 468f, 1f),
        (KeyCode.Y, 6.5f, 468f, 1f), (KeyCode.U, 7.5f, 468f, 1f), (KeyCode.I, 8.5f, 468f, 1f),
        (KeyCode.O, 9.5f, 468f, 1f), (KeyCode.P, 10.5f, 468f, 1f), (KeyCode.LeftBracket, 11.5f, 468f, 1f),
        (KeyCode.RightBracket, 12.5f, 468f, 1f), (KeyCode.Backslash, 13.5f, 468f, 1.5f),
        (KeyCode.CapsLock, 0f, 412f, 1.75f), (KeyCode.A, 1.75f, 412f, 1f), (KeyCode.S, 2.75f, 412f, 1f),
        (KeyCode.D, 3.75f, 412f, 1f), (KeyCode.F, 4.75f, 412f, 1f), (KeyCode.G, 5.75f, 412f, 1f),
        (KeyCode.H, 6.75f, 412f, 1f), (KeyCode.J, 7.75f, 412f, 1f), (KeyCode.K, 8.75f, 412f, 1f),
        (KeyCode.L, 9.75f, 412f, 1f), (KeyCode.Semicolon, 10.75f, 412f, 1f), (KeyCode.Quote, 11.75f, 412f, 1f),
        (KeyCode.Return, 12.75f, 412f, 2.25f),
        (KeyCode.LeftShift, 0f, 356f, 2.25f), (KeyCode.Z, 2.25f, 356f, 1f), (KeyCode.X, 3.25f, 356f, 1f),
        (KeyCode.C, 4.25f, 356f, 1f), (KeyCode.V, 5.25f, 356f, 1f), (KeyCode.B, 6.25f, 356f, 1f),
        (KeyCode.N, 7.25f, 356f, 1f), (KeyCode.M, 8.25f, 356f, 1f), (KeyCode.Comma, 9.25f, 356f, 1f),
        (KeyCode.Period, 10.25f, 356f, 1f), (KeyCode.Slash, 11.25f, 356f, 1f),
        (KeyCode.RightShift, 12.25f, 356f, 2.75f),
        (KeyCode.LeftControl, 0f, 300f, 1.25f), (KeyCode.LeftWindows, 1.25f, 300f, 1.25f),
        (KeyCode.LeftAlt, 2.5f, 300f, 1.25f), (KeyCode.Space, 3.75f, 300f, 6.25f),
        (KeyCode.RightAlt, 10f, 300f, 1.25f), (KeyCode.RightWindows, 11.25f, 300f, 1.25f),
        (KeyCode.Menu, 12.5f, 300f, 1.25f), (KeyCode.RightControl, 13.75f, 300f, 1.25f),
        (KeyCode.Insert, 19f, 524f, 1f), (KeyCode.Delete, 19f, 468f, 1f),
        (KeyCode.Home, 20f, 524f, 1f), (KeyCode.End, 20f, 468f, 1f),
        (KeyCode.PageUp, 21f, 524f, 1f), (KeyCode.PageDown, 21f, 468f, 1f),
        (KeyCode.UpArrow, 20f, 356f, 1f), (KeyCode.LeftArrow, 19f, 300f, 1f),
        (KeyCode.DownArrow, 20f, 300f, 1f), (KeyCode.RightArrow, 21f, 300f, 1f),
        (KeyCode.Numlock, 22f, 524f, 1f), (KeyCode.KeypadDivide, 23f, 524f, 1f),
        (KeyCode.KeypadMultiply, 24f, 524f, 1f), (KeyCode.KeypadMinus, 25f, 524f, 1f),
        (KeyCode.Keypad7, 22f, 468f, 1f), (KeyCode.Keypad8, 23f, 468f, 1f),
        (KeyCode.Keypad9, 24f, 468f, 1f), (KeyCode.KeypadPlus, 25f, 468f, 1f),
        (KeyCode.Keypad4, 22f, 412f, 1f), (KeyCode.Keypad5, 23f, 412f, 1f), (KeyCode.Keypad6, 24f, 412f, 1f),
        (KeyCode.Keypad1, 22f, 356f, 1f), (KeyCode.Keypad2, 23f, 356f, 1f), (KeyCode.Keypad3, 24f, 356f, 1f),
        (KeyCode.Keypad0, 22f, 300f, 2f), (KeyCode.KeypadPeriod, 24f, 300f, 1f),
        (KeyCode.KeypadEnter, 25f, 356f, 1f),
    ];
    internal static void Generate108KeyTab(KvDocument doc, string tab) {
        if(doc == null) return;
        doc.EnsureTab(tab);
        doc.Clear(tab);
        KvKeyStyle look = StyleFor(Stock, 0, 1);
        look.Counter = false;
        look.NoteEffect = false;
        float z = 0f;
        foreach((KeyCode key, float cx, float jy, float wu) in Full108Slots) {
            bool tall = key is KeyCode.KeypadPlus or KeyCode.KeypadEnter;
            float x = cx * Col108 - (cx >= RightCol108 ? RightShift108 : 0f);
            float y = TopRow108 - jy;
            float w = wu * Col108 - Gap108;
            float h = tall ? 2f * Col108 - Gap108 : KeyW;
            KvElement el = Add(doc, tab, KvElementKind.Key, x, y, w, h, look, z++);
            el.BindKey(key);
        }
        KvKeyStyle statLook = StyleFor(Stock, StatSlot, 1);
        float statY = TopRow108 - 300f + KeyW + FootGapAbove;
        AddStat108(doc, tab, "kps", "KPS", 2f * Col108, statY, statLook, ref z);
        AddStat108(doc, tab, "total", "Total", 5f * Col108, statY, statLook, ref z);
        doc.ReindexZOrder(tab);
    }
    private static void AddStat108(KvDocument doc, string tab, string type, string caption,
        float x, float y, in KvKeyStyle look, ref float z) {
        KvElement el = Add(doc, tab, KvElementKind.Stat, x, y, 3f * Col108 - Gap108, StatH108, look, z++);
        el.StatType = type;
        el.DisplayText = caption;
        ApplyStatCounter(el, 2, Stock.StatsTogether);
    }
}

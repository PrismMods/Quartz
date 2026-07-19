using UnityEngine;
namespace Quartz.Features.KeyViewer.Layout;
/// <summary>
/// The full 108-key physical keyboard preset, ported from JipperKeyViewer's Full108 layout.
/// Kept out of <see cref="KvPresets"/>'s main file only for length — it is the same class.
/// </summary>
internal static partial class KvPresets {
    // JipperKeyViewer packs the board at a 56px column step (a 50px key + a 6px gap) and the
    // same 6px gap between rows, so the whole keyboard carries uniform 6px gaps. Matched here so
    // the ported stagger lands to the pixel.
    private const float Col108 = 56f;   // 50px key + 6px gap: one key-unit of horizontal room
    private const float Gap108 = 6f;    // trimmed off each key's width to leave the gap
    private const float StatH108 = 30f; // compact stat height, matching KeyViewerOverlay.CompactStatH
    /// <summary>
    /// JipperKeyViewer authors the board in its own y-up space, where the function row sits at
    /// 580 and each row below it is 56px lower (down to the space row at 300). Quartz's document
    /// is y-down, so the port flips each row with <c>y = TopRow108 - jy</c>: the function row
    /// lands at 0 and every row below it one pitch further down.
    /// </summary>
    private const float TopRow108 = 580f;
    /// <summary>
    /// First column of the edit / arrow / numpad cluster. Everything at or past it is the right
    /// block, pulled left by <see cref="RightShift108"/> so its left edge hugs the main board's
    /// right edge instead of leaving JipperKeyViewer's authoring gap.
    /// </summary>
    private const float RightCol108 = 19f;
    private const float RightShift108 = 4f * Col108;
    /// <summary>
    /// One physical key: its bound code, column (in key-units from the left edge), row-y (in
    /// JipperKeyViewer's y-up space) and width (in key-units). Index order follows a real
    /// keyboard: function row, number row, QWERTY, home row, ZXCV, bottom row, edit cluster,
    /// arrows, numpad.
    ///
    /// The numpad "+" and Enter are the two double-height keys; their row-y is the TOP of the two
    /// rows they span. JipperKeyViewer instead stores a centered y and reconstructs the extent
    /// from its RectTransform pivot — the top-row form is exact in Quartz's top-left box model and
    /// needs no pivot. Every other key is one row (50px) tall.
    /// </summary>
    private static readonly (KeyCode Key, float Cx, float Jy, float Wu)[] Full108Slots = [
        // Function row
        (KeyCode.Escape, 0f, 580f, 1f),
        (KeyCode.F1, 2f, 580f, 1f), (KeyCode.F2, 3f, 580f, 1f), (KeyCode.F3, 4f, 580f, 1f),
        (KeyCode.F4, 5f, 580f, 1f), (KeyCode.F5, 6.5f, 580f, 1f), (KeyCode.F6, 7.5f, 580f, 1f),
        (KeyCode.F7, 8.5f, 580f, 1f), (KeyCode.F8, 9.5f, 580f, 1f), (KeyCode.F9, 11f, 580f, 1f),
        (KeyCode.F10, 12f, 580f, 1f), (KeyCode.F11, 13f, 580f, 1f), (KeyCode.F12, 14f, 580f, 1f),
        // PrtSc, ScrLk, Pause — the three-key cluster a real function row ends with. (JipperKeyViewer
        // also placed a SysReq key here, but SysReq IS the Print Screen key's shifted face, not a
        // separate key, so it rendered as a second "PrtSc"; dropped.)
        (KeyCode.Print, 15f, 580f, 1f), (KeyCode.ScrollLock, 16f, 580f, 1f),
        (KeyCode.Pause, 17f, 580f, 1f),
        // Number row
        (KeyCode.BackQuote, 0f, 524f, 1f), (KeyCode.Alpha1, 1f, 524f, 1f), (KeyCode.Alpha2, 2f, 524f, 1f),
        (KeyCode.Alpha3, 3f, 524f, 1f), (KeyCode.Alpha4, 4f, 524f, 1f), (KeyCode.Alpha5, 5f, 524f, 1f),
        (KeyCode.Alpha6, 6f, 524f, 1f), (KeyCode.Alpha7, 7f, 524f, 1f), (KeyCode.Alpha8, 8f, 524f, 1f),
        (KeyCode.Alpha9, 9f, 524f, 1f), (KeyCode.Alpha0, 10f, 524f, 1f), (KeyCode.Minus, 11f, 524f, 1f),
        (KeyCode.Equals, 12f, 524f, 1f), (KeyCode.Backspace, 13f, 524f, 2f),
        // QWERTY row
        (KeyCode.Tab, 0f, 468f, 1.5f), (KeyCode.Q, 1.5f, 468f, 1f), (KeyCode.W, 2.5f, 468f, 1f),
        (KeyCode.E, 3.5f, 468f, 1f), (KeyCode.R, 4.5f, 468f, 1f), (KeyCode.T, 5.5f, 468f, 1f),
        (KeyCode.Y, 6.5f, 468f, 1f), (KeyCode.U, 7.5f, 468f, 1f), (KeyCode.I, 8.5f, 468f, 1f),
        (KeyCode.O, 9.5f, 468f, 1f), (KeyCode.P, 10.5f, 468f, 1f), (KeyCode.LeftBracket, 11.5f, 468f, 1f),
        (KeyCode.RightBracket, 12.5f, 468f, 1f), (KeyCode.Backslash, 13.5f, 468f, 1.5f),
        // Home row
        (KeyCode.CapsLock, 0f, 412f, 1.75f), (KeyCode.A, 1.75f, 412f, 1f), (KeyCode.S, 2.75f, 412f, 1f),
        (KeyCode.D, 3.75f, 412f, 1f), (KeyCode.F, 4.75f, 412f, 1f), (KeyCode.G, 5.75f, 412f, 1f),
        (KeyCode.H, 6.75f, 412f, 1f), (KeyCode.J, 7.75f, 412f, 1f), (KeyCode.K, 8.75f, 412f, 1f),
        (KeyCode.L, 9.75f, 412f, 1f), (KeyCode.Semicolon, 10.75f, 412f, 1f), (KeyCode.Quote, 11.75f, 412f, 1f),
        (KeyCode.Return, 12.75f, 412f, 2.25f),
        // ZXCV row
        (KeyCode.LeftShift, 0f, 356f, 2.25f), (KeyCode.Z, 2.25f, 356f, 1f), (KeyCode.X, 3.25f, 356f, 1f),
        (KeyCode.C, 4.25f, 356f, 1f), (KeyCode.V, 5.25f, 356f, 1f), (KeyCode.B, 6.25f, 356f, 1f),
        (KeyCode.N, 7.25f, 356f, 1f), (KeyCode.M, 8.25f, 356f, 1f), (KeyCode.Comma, 9.25f, 356f, 1f),
        (KeyCode.Period, 10.25f, 356f, 1f), (KeyCode.Slash, 11.25f, 356f, 1f),
        (KeyCode.RightShift, 12.25f, 356f, 2.75f),
        // Bottom row
        (KeyCode.LeftControl, 0f, 300f, 1.25f), (KeyCode.LeftWindows, 1.25f, 300f, 1.25f),
        (KeyCode.LeftAlt, 2.5f, 300f, 1.25f), (KeyCode.Space, 3.75f, 300f, 6.25f),
        (KeyCode.RightAlt, 10f, 300f, 1.25f), (KeyCode.RightWindows, 11.25f, 300f, 1.25f),
        (KeyCode.Menu, 12.5f, 300f, 1.25f), (KeyCode.RightControl, 13.75f, 300f, 1.25f),
        // Edit cluster
        (KeyCode.Insert, 19f, 524f, 1f), (KeyCode.Delete, 19f, 468f, 1f),
        (KeyCode.Home, 20f, 524f, 1f), (KeyCode.End, 20f, 468f, 1f),
        (KeyCode.PageUp, 21f, 524f, 1f), (KeyCode.PageDown, 21f, 468f, 1f),
        // Arrow keys
        (KeyCode.UpArrow, 20f, 356f, 1f), (KeyCode.LeftArrow, 19f, 300f, 1f),
        (KeyCode.DownArrow, 20f, 300f, 1f), (KeyCode.RightArrow, 21f, 300f, 1f),
        // Numpad
        (KeyCode.Numlock, 22f, 524f, 1f), (KeyCode.KeypadDivide, 23f, 524f, 1f),
        (KeyCode.KeypadMultiply, 24f, 524f, 1f), (KeyCode.KeypadMinus, 25f, 524f, 1f),
        (KeyCode.Keypad7, 22f, 468f, 1f), (KeyCode.Keypad8, 23f, 468f, 1f),
        (KeyCode.Keypad9, 24f, 468f, 1f), (KeyCode.KeypadPlus, 25f, 468f, 1f), // "+" spans rows 468 & 412
        (KeyCode.Keypad4, 22f, 412f, 1f), (KeyCode.Keypad5, 23f, 412f, 1f), (KeyCode.Keypad6, 24f, 412f, 1f),
        (KeyCode.Keypad1, 22f, 356f, 1f), (KeyCode.Keypad2, 23f, 356f, 1f), (KeyCode.Keypad3, 24f, 356f, 1f),
        (KeyCode.Keypad0, 22f, 300f, 2f), (KeyCode.KeypadPeriod, 24f, 300f, 1f),
        (KeyCode.KeypadEnter, 25f, 356f, 1f), // Enter spans rows 356 & 300
    ];
    /// <summary>
    /// Lay the 108-key physical keyboard onto <paramref name="tab"/>, replacing whatever is there.
    ///
    /// Built directly rather than through GenerateKeyLayout: every key binds itself from
    /// <see cref="Full108Slots"/> (there is no legacy slice to bake), the same way
    /// <see cref="Generate24KeyTab"/>'s extra row does. Matches JipperKeyViewer's Full108, which
    /// wears no per-key colors, shows each key's letter instead of a press counter, and spawns no
    /// rain — all still editable afterwards, since a preset is only a starting point.
    /// </summary>
    internal static void Generate108KeyTab(KvDocument doc, string tab) {
        if(doc == null) return;
        doc.EnsureTab(tab);
        doc.Clear(tab);
        // One uniform stock main-key look. Counter off and rain off to match JipperKeyViewer's
        // Full108 (which draws labels and passes rainRow -1); both are mutated on the local copy,
        // not baked into StyleFor, so the other presets are untouched.
        KvKeyStyle look = StyleFor(Stock, 0, 1);
        look.Counter = false;
        look.NoteEffect = false;
        float z = 0f;
        foreach((KeyCode key, float cx, float jy, float wu) in Full108Slots) {
            bool tall = key is KeyCode.KeypadPlus or KeyCode.KeypadEnter;
            float x = cx * Col108 - (cx >= RightCol108 ? RightShift108 : 0f);
            float y = TopRow108 - jy;
            float w = wu * Col108 - Gap108;
            // A tall key is two rows high: two column steps minus the one gap it would leave.
            float h = tall ? 2f * Col108 - Gap108 : KeyW;
            KvElement el = Add(doc, tab, KvElementKind.Key, x, y, w, h, look, z++);
            el.BindKey(key);
        }
        // A KPS and a Total readout below the space row, the way every other preset carries them,
        // centered under the left of the alphanumeric block.
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
        // Style 2's caption-left / value-right arrangement, honoring the stock Together setting.
        ApplyStatCounter(el, 2, Stock.StatsTogether);
    }
}

using System.Globalization;
using UnityEngine;
using TMPro;
namespace Quartz.Features.KeyViewer;
public static partial class KeyViewerOverlay {
    private static string FormatCount(int value) =>
        Conf != null && Conf.CountFormatting
            ? value.ToString("N0", CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);
    private static readonly char[] countBuf = new char[16];
    private static char[] prefixedCountBuf = new char[32];
    private static void SetCount(TextMeshProUGUI tmp, int value)
        => SetCount(tmp, value, Conf != null && Conf.CountFormatting);
    private static void SetCount(TextMeshProUGUI tmp, int value, bool thousands) {
        int pos = WriteCountDigits(value, thousands);
        tmp.SetText(countBuf, pos, countBuf.Length - pos);
    }
    private static void SetPrefixedCount(TextMeshProUGUI tmp, char[] prefix, int value)
        => SetPrefixedCount(tmp, prefix, value, Conf != null && Conf.CountFormatting);
    private static void SetPrefixedCount(TextMeshProUGUI tmp, char[] prefix, int value, bool thousands) {
        int pos = WriteCountDigits(value, thousands);
        int digits = countBuf.Length - pos;
        int len = prefix.Length + digits;
        if(prefixedCountBuf.Length < len) prefixedCountBuf = new char[len * 2];
        Array.Copy(prefix, prefixedCountBuf, prefix.Length);
        Array.Copy(countBuf, pos, prefixedCountBuf, prefix.Length, digits);
        tmp.SetText(prefixedCountBuf, 0, len);
    }
    private static int WriteCountDigits(int value, bool thousands) {
        int pos = countBuf.Length;
        if(value == 0) {
            countBuf[--pos] = '0';
        } else {
            long v = value;
            bool neg = v < 0;
            if(neg) v = -v;
            int seg = 0;
            while(v > 0) {
                if(thousands && seg == 3) { countBuf[--pos] = ','; seg = 0; }
                countBuf[--pos] = (char)('0' + (int)(v % 10));
                v /= 10;
                seg++;
            }
            if(neg) countBuf[--pos] = '-';
        }
        return pos;
    }
}

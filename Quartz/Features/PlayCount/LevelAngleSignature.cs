using System.Globalization;
using System.Text;
namespace Quartz.Features.PlayCount;
public static class LevelAngleSignature {
    public static string Extract(string text) {
        if(string.IsNullOrEmpty(text)) return null;
        int i = text.IndexOf("\"angleData\"", StringComparison.Ordinal);
        if(i >= 0) {
            int open = text.IndexOf('[', i);
            int close = open >= 0 ? text.IndexOf(']', open) : -1;
            if(close > open) {
                string angles = Normalize(text.Substring(open + 1, close - open - 1));
                if(angles != null) return angles;
            }
        }
        i = text.IndexOf("\"pathData\"", StringComparison.Ordinal);
        if(i >= 0) {
            int open = text.IndexOf('"', i + 10);
            int close = open >= 0 ? text.IndexOf('"', open + 1) : -1;
            if(close > open) return "path:" + text.Substring(open + 1, close - open - 1);
        }
        return null;
    }
    private static string Normalize(string raw) {
        string[] parts = raw.Split(',');
        StringBuilder sb = new();
        sb.Append("angles:").Append(parts.Length);
        for(int i = 0; i < parts.Length; i++) {
            if(!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return null;
            sb.Append(':').Append(v.ToString("0.###", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}

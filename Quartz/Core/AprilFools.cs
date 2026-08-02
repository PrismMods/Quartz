using System;
using System.Text;
using System.Threading;
namespace Quartz.Core;
public static class AprilFools {
    public const string BrandName = "Quiz";
    private const int RecheckMs = 60_000;
    private static volatile bool onCalendar;
    private static volatile bool primed;
    private static int checkedAt;
    public static bool DevForced { get; private set; }
    public static void SetDevForced(bool on) => DevForced = on;
    public static bool Active => (Info.IsDev && DevForced) || OnCalendar;
    private static bool OnCalendar {
        get {
            int now = Environment.TickCount;
            if(primed && unchecked(now - Volatile.Read(ref checkedAt)) is >= 0 and < RecheckMs) return onCalendar;
            DateTime today = DateTime.Now;
            onCalendar = today.Month == 4 && today.Day == 1;
            Volatile.Write(ref checkedAt, now);
            primed = true;
            return onCalendar;
        }
    }
    public static string Rebrand(string text) {
        if(string.IsNullOrEmpty(text) || !Active) return text;
        int start = NextBrand(text, 0);
        if(start < 0) return text;
        StringBuilder builder = new(text.Length);
        int cursor = 0;
        while(start >= 0) {
            builder.Append(text, cursor, start - cursor).Append(BrandName);
            cursor = start + Info.Name.Length;
            start = NextBrand(text, cursor);
        }
        return builder.Append(text, cursor, text.Length - cursor).ToString();
    }
    private static int NextBrand(string text, int from) {
        for(int i = text.IndexOf(Info.Name, from, StringComparison.Ordinal); i >= 0;
            i = text.IndexOf(Info.Name, i + Info.Name.Length, StringComparison.Ordinal)) {
            int after = i + Info.Name.Length;
            char previous = i > 0 ? text[i - 1] : '\0';
            char next = after < text.Length ? text[after] : '\0';
            char second = after + 1 < text.Length ? text[after + 1] : '\0';
            if(Blocked(previous) || Blocked(next)) continue;
            if(next is '.' or '-' or '_' && Identifier(second)) continue;
            return i;
        }
        return -1;
    }
    private static bool Blocked(char c) => c is '/' or '\\' || Identifier(c);
    private static bool Identifier(char c)
        => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');
}

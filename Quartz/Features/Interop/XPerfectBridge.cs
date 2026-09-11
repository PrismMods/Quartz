using System.Reflection;
using Quartz.Compat.Game;
using Quartz.Core;
namespace Quartz.Features.Interop;
public static class XPerfectBridge {
    public enum Judge {
        None = 0,
        X = 1,
        Plus = 2,
        Minus = 3,
    }
    private static bool installed;
    private static bool hookInstalled;
    private static bool assembliesChanged = true;
    private static MemberInfo lastJudgeMember;
    private static MemberInfo lastJudgeForTextMember;
    private static MemberInfo xCountMember;
    private static MemberInfo plusCountMember;
    private static MemberInfo minusCountMember;
    private static PropertyInfo enabledProp;
    private static Func<int> xCountFast;
    private static Func<int> plusCountFast;
    private static Func<int> minusCountFast;
    private static Func<bool> enabledFast;
    private static Func<T> BindStatic<T>(MemberInfo member) {
        try {
            if(member is PropertyInfo p && p.PropertyType == typeof(T))
                return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), p.GetGetMethod(true));
            if(member is FieldInfo f && f.FieldType == typeof(T)) {
                HarmonyLib.AccessTools.FieldRef<T> field = HarmonyLib.AccessTools.StaticFieldRefAccess<T>(f);
                return field == null ? null : () => field();
            }
        } catch(Exception e) { Diag.Ignore(e); }
        return null;
    }
    private static readonly int NativeX = GameIndex("XPerfect");
    private static readonly int NativePlus = GameIndex("PerfectPlus");
    private static readonly int NativeMinus = GameIndex("PerfectMinus");
    private static readonly Refl.Member PerfectTextPreset = new(typeof(Persistence), "hitMarginPerfectText");
    private static int GameIndex(string name) {
        try {
            return Enum.IsDefined(typeof(HitMargin), name) ? Convert.ToInt32(Enum.Parse(typeof(HitMargin), name)) : -1;
        } catch(Exception e) {
            Diag.Ignore(e);
            return -1;
        }
    }
    public static bool Native => NativeX >= 0;
    private static readonly Refl.Member HitMarginColours = new(typeof(RDC), "hitMarginColoursBySettings");
    private static Refl.Member xPerfectColourMember;
    public static UnityEngine.Color NativeXColor() {
        object scheme = HitMarginColours.Get(null);
        if(scheme == null) return UnityEngine.Color.white;
        xPerfectColourMember ??= new Refl.Member(scheme.GetType(), "colourXPerfect");
        return xPerfectColourMember.Get(scheme) is UnityEngine.Color c ? c : UnityEngine.Color.white;
    }
    public static bool Installed {
        get {
            if(Native) return true;
            EnsureResolved();
            return installed;
        }
    }
    private static int activeFrame = -1;
    private static bool activeCache;
    public static bool Active {
        get {
            if(!Installed) return false;
            if(activeFrame == UnityEngine.Time.frameCount) return activeCache;
            bool result;
            try {
                result = Native ? Convert.ToInt32(PerfectTextPreset.Get(null) ?? 0) != 0
                    : enabledFast != null ? enabledFast()
                    : enabledProp == null || (enabledProp.GetValue(null, null) is bool b && b);
            } catch(Exception e) {
                Diag.Ignore(e);
                result = false;
            }
            activeFrame = UnityEngine.Time.frameCount;
            activeCache = result;
            return result;
        }
    }
    public static Judge LastJudge() => ReadJudge(lastJudgeMember, Judge.None);
    public static Judge LastJudgeForText() =>
        lastJudgeForTextMember == null ? LastJudge() : ReadJudge(lastJudgeForTextMember, LastJudge());
    public static Judge JudgeFor(HitMargin hit) => Native ? NativeJudge(hit) : LastJudge();
    public static Judge JudgeForText(HitMargin hit) => Native ? NativeJudge(hit) : LastJudgeForText();
    private static Judge NativeJudge(HitMargin hit) {
        int i = (int)hit;
        return i == NativeX ? Judge.X : i == NativePlus ? Judge.Plus : i == NativeMinus ? Judge.Minus : Judge.None;
    }
    public static int EarlyCount() => Native ? MinusCount() : PlusCount();
    public static int LateCount() => Native ? PlusCount() : MinusCount();
    private static int countsFrame = -1;
    private static int xCountCache;
    private static int plusCountCache;
    private static int minusCountCache;
    public static int XCount() {
        RefreshCounts();
        return xCountCache;
    }
    public static int PlusCount() {
        RefreshCounts();
        return plusCountCache;
    }
    public static int MinusCount() {
        RefreshCounts();
        return minusCountCache;
    }
    private static void RefreshCounts() {
        if(countsFrame == UnityEngine.Time.frameCount) return;
        if(Native) {
            int[] counts = GameApi.HitMarginCounts(GameApi.Tracker(0));
            xCountCache = NativeCount(counts, NativeX);
            plusCountCache = NativeCount(counts, NativePlus);
            minusCountCache = NativeCount(counts, NativeMinus);
            countsFrame = UnityEngine.Time.frameCount;
            return;
        }
        xCountCache = xCountFast != null ? xCountFast() : ReadIntMember(xCountMember);
        plusCountCache = plusCountFast != null ? plusCountFast() : ReadIntMember(plusCountMember);
        minusCountCache = minusCountFast != null ? minusCountFast() : ReadIntMember(minusCountMember);
        countsFrame = UnityEngine.Time.frameCount;
    }
    private static int NativeCount(int[] counts, int index) =>
        counts != null && index >= 0 && index < counts.Length ? counts[index] : 0;
    private static Judge ReadJudge(MemberInfo member, Judge fallback) {
        if(!Installed || member == null) return fallback;
        try {
            object v = ReadStaticMember(member);
            if(v == null) return Judge.None;
            int i = System.Convert.ToInt32(v);
            return i is < 0 or > 3 ? Judge.None : (Judge)i;
        } catch(Exception e) {
            Diag.Ignore(e);
            return fallback;
        }
    }
    private static int ReadIntMember(MemberInfo member) {
        if(!Installed || member == null) return 0;
        try {
            object v = ReadStaticMember(member);
            return v == null ? 0 : System.Convert.ToInt32(v);
        } catch(Exception e) {
            Diag.Ignore(e);
            return 0;
        }
    }
    private static object ReadStaticMember(MemberInfo member) {
        if(member is PropertyInfo property) return property.GetValue(null, null);
        return member is FieldInfo field ? field.GetValue(null) : null;
    }
    private static MemberInfo GetStaticReadable(Type type, string name) {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        PropertyInfo property = type.GetProperty(name, flags);
        if(property != null && property.GetGetMethod(true) != null) return property;
        FieldInfo field = type.GetField(name, flags);
        return field ?? type.GetField("<" + name + ">k__BackingField", flags);
    }
    private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args) => assembliesChanged = true;
    internal static void Unhook() {
        if(!hookInstalled) return;
        hookInstalled = false;
        try {
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        } catch(Exception e) { Diag.Ignore(e); }
    }
    private static void EnsureResolved() {
        if(installed) return;
        if(!hookInstalled) {
            hookInstalled = true;
            try {
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            } catch(Exception e) { Diag.Ignore(e); }
        }
        if(!assembliesChanged) return;
        assembliesChanged = false;
        try {
            Assembly xpAsm = null;
            foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies()) {
                if(a.GetName().Name == "XPerfect") {
                    xpAsm = a;
                    break;
                }
            }
            if(xpAsm == null) return;
            Type accuracyStateType = xpAsm.GetType("XPerfect.AccuracyState");
            if(accuracyStateType == null) return;
            lastJudgeMember = GetStaticReadable(accuracyStateType, "LastJudge");
            lastJudgeForTextMember = GetStaticReadable(accuracyStateType, "LastJudgeForText");
            xCountMember = GetStaticReadable(accuracyStateType, "XPerfectCount");
            plusCountMember = GetStaticReadable(accuracyStateType, "PlusPerfectCount");
            minusCountMember = GetStaticReadable(accuracyStateType, "MinusPerfectCount");
            Type mainType = xpAsm.GetType("XPerfect.Main");
            if(mainType != null) enabledProp = mainType.GetProperty("Enabled", BindingFlags.Public | BindingFlags.Static);
            xCountFast = BindStatic<int>(xCountMember);
            plusCountFast = BindStatic<int>(plusCountMember);
            minusCountFast = BindStatic<int>(minusCountMember);
            enabledFast = BindStatic<bool>(enabledProp);
            installed = lastJudgeMember != null;
            if(installed) Unhook();
        } catch(Exception e) { Diag.Ignore(e); }
    }
}

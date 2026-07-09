using System.Reflection;
namespace Quartz.Features.Interop;
internal static class XPerfectBridge {
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
    public static bool Installed {
        get {
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
                result = enabledProp == null || (enabledProp.GetValue(null, null) is bool b && b);
            } catch {
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
        xCountCache = ReadIntMember(xCountMember);
        plusCountCache = ReadIntMember(plusCountMember);
        minusCountCache = ReadIntMember(minusCountMember);
        countsFrame = UnityEngine.Time.frameCount;
    }
    private static Judge ReadJudge(MemberInfo member, Judge fallback) {
        if(!Installed || member == null) return fallback;
        try {
            object v = ReadStaticMember(member);
            if(v == null) return Judge.None;
            int i = System.Convert.ToInt32(v);
            return i is < 0 or > 3 ? Judge.None : (Judge)i;
        } catch {
            return fallback;
        }
    }
    private static int ReadIntMember(MemberInfo member) {
        if(!Installed || member == null) return 0;
        try {
            object v = ReadStaticMember(member);
            return v == null ? 0 : System.Convert.ToInt32(v);
        } catch {
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
    private static void EnsureResolved() {
        if(installed) return;
        if(!hookInstalled) {
            hookInstalled = true;
            try {
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            } catch { }
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
            installed = lastJudgeMember != null;
            if(installed) {
                try {
                    AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                } catch { }
            }
        } catch {
        }
    }
}

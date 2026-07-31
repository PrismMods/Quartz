using System.Reflection;
using Quartz.Compat.Game;
using static Asserts;
static class ReflTests {
#pragma warning disable CS0414
    class Base {
        public object Shadowed { get; set; }
        public string BaseOnly { get; set; }
        public int BaseField = 3;
    }
    class Derived : Base {
        public new string Shadowed { get; set; }
        public string ReadOnlyProp => "fixed";
        public string WriteTracked { private get; set; }
        public string LastWritten = null;
        public string this[int i] => "indexer";
        private int Hidden = 7;
        public static string StaticProp { get; set; }
    }
#pragma warning restore CS0414
    class Thrower {
        public string Boom => throw new InvalidOperationException("property blew up");
        public void Explode() => throw new InvalidOperationException("method blew up");
    }
    class Methods {
        public string Zero() => "zero";
        public string One(int a) => "one:" + a;
        public string Two(int a, string b) => "two:" + a + b;
        public string Optionals(int a, string b = "def", int c = 5) => "opt:" + a + b + c;
        public static string Stat(int a) => "stat:" + a;
    }
    public static void TestResolvesPropertiesAndFields() {
        Refl.Member baseOnly = new(typeof(Derived), "BaseOnly");
        Assert(baseOnly.Exists, "an inherited property resolves from the derived type");
        Refl.Member field = new(typeof(Derived), "BaseField");
        Assert(field.Exists, "an inherited field resolves from the derived type");
        Refl.Member privateField = new(typeof(Derived), "Hidden");
        Assert(privateField.Get<int>(new Derived()) == 7, "a private field is readable");
        Refl.Member statik = new(typeof(Derived), "StaticProp");
        Derived.StaticProp = "s";
        Assert(statik.Get<string>(null) == "s", "a static property reads with a null instance");
    }
    public static void TestAmbiguousMemberFallsBackToTheNearestDeclaration() {
        Assert(Throws(() => typeof(Derived).GetProperty("Shadowed", BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.FlattenHierarchy)), "the fixture really is ambiguous to plain reflection");
        Refl.Member m = new(typeof(Derived), "Shadowed");
        Assert(m.Exists, "an ambiguous member still resolves");
        Derived d = new();
        d.Shadowed = "derived";
        ((Base)d).Shadowed = "base";
        Assert(m.Get<string>(d) == "derived", "the walk picks the most-derived declaration");
    }
    public static void TestNameListTakesTheFirstThatExists() {
        Refl.Member m = new(typeof(Derived), "NoSuchName", "BaseOnly");
        Assert(m.Exists, "a later name in the list resolves when the first is absent");
        Derived d = new() { BaseOnly = "hit" };
        Assert(m.Get<string>(d) == "hit", "the resolved name is the one that exists");
    }
    public static void TestMissingMembersAreInertNotFatal() {
        Refl.Member missing = new(typeof(Derived), "NotAThing");
        Assert(!missing.Exists, "an absent member reports Exists false");
        Assert(missing.Get(new Derived()) == null, "reading an absent member yields null");
        Assert(missing.Get(new Derived(), "fallback") == "fallback", "the typed fallback is returned");
        missing.Set(new Derived(), "ignored");
        Refl.Member nullOwner = new(null, "Anything");
        Assert(!nullOwner.Exists, "a null owner resolves nothing");
        Assert(nullOwner.Get(null) == null, "reading off a null owner yields null");
        nullOwner.Set(null, "ignored");
    }
    public static void TestIndexersAreNeverTreatedAsMembers() {
        Refl.Member m = new(typeof(Derived), "Item");
        Assert(!m.Exists, "an indexer is skipped rather than resolved as a property");
    }
    public static void TestTypedGetFallsBackOnMismatch() {
        Refl.Member m = new(typeof(Derived), "BaseOnly");
        Derived d = new() { BaseOnly = "text" };
        Assert(m.Get(d, -1) == -1, "a wrong-typed read returns the fallback, not a cast failure");
        Assert(m.Get<string>(d) == "text", "a right-typed read returns the value");
    }
    public static void TestWritesGoThroughAndUnwritableTargetsAreInert() {
        Derived d = new();
        Refl.Member writable = new(typeof(Derived), "LastWritten");
        writable.Set(d, "written");
        Assert(d.LastWritten == "written", "a settable field is written");
        Refl.Member readOnly = new(typeof(Derived), "ReadOnlyProp");
        readOnly.Set(d, "nope");
        Assert(d.ReadOnlyProp == "fixed", "a get-only property is left alone instead of throwing");
        Refl.Member setOnly = new(typeof(Derived), "WriteTracked");
        setOnly.Set(d, "via-setter");
        Assert(setOnly.Exists, "a private-get property still resolves");
    }
    public static void TestThrowingMembersAreSwallowedNotPropagated() {
        Refl.Member boom = new(typeof(Thrower), "Boom");
        Assert(boom.Exists, "the throwing property resolves");
        Assert(boom.Get(new Thrower()) == null, "a property getter that throws yields null");
    }
    public static void TestMethodSelectionByArgCount() {
        Assert(Refl.Method(typeof(Methods), "One", 1) != null, "an exact arity match resolves");
        Assert(Refl.Method(typeof(Methods), "Two", 2).GetParameters().Length == 2, "the two-arg overload is picked");
        Assert(Refl.Method(typeof(Methods), "Zero", 0) != null, "a zero-arg method resolves");
        Assert(Refl.Method(typeof(Methods), "Stat", 1) != null, "a static method resolves");
        Assert(Refl.Method(typeof(Methods), "Nope", 0) == null, "an absent method resolves to null");
        Assert(Refl.Method(null, "One", 1) == null, "a null owner resolves to null");
        Assert(Refl.Method(typeof(Methods), "", 1) == null, "an empty name resolves to null");
        Assert(Refl.Method(typeof(Methods), "One") != null, "a negative arg count takes the first match");
    }
    public static void TestOptionalParametersSatisfyAShorterArgCount() {
        MethodInfo m = Refl.Method(typeof(Methods), "Optionals", 1);
        Assert(m != null, "a method whose extra parameters are all optional matches a shorter arity");
        Assert(m.GetParameters().Length == 3, "the resolved method keeps its full signature");
    }
    public static void TestMethodLookupIsCached() {
        MethodInfo first = Refl.Method(typeof(Methods), "One", 1);
        MethodInfo second = Refl.Method(typeof(Methods), "One", 1);
        Assert(ReferenceEquals(first, second), "repeat lookups return the cached MethodInfo");
        Assert(Refl.Method(typeof(Methods), "Missing", 0) == null
            && Refl.Method(typeof(Methods), "Missing", 0) == null,
            "a negative result caches without turning into a hit");
    }
    public static void TestInvokePadsMissingOptionalArguments() {
        Methods target = new();
        MethodInfo m = Refl.Method(typeof(Methods), "Optionals", 1);
        Assert((string)Refl.Invoke(m, target, 1) == "opt:1def5", "omitted optionals are padded with their defaults");
        Assert((string)Refl.Invoke(m, target, 1, "x") == "opt:1x5", "a partially supplied call pads only the tail");
        Assert((string)Refl.Invoke(m, target, 1, "x", 9) == "opt:1x9", "a fully supplied call is passed through");
    }
    public static void TestInvokeIsInertOnNullAndThrowingTargets() {
        Assert(Refl.Invoke(null, new Methods()) == null, "invoking a null method yields null");
        MethodInfo explode = Refl.Method(typeof(Thrower), "Explode", 0);
        Assert(Refl.Invoke(explode, new Thrower()) == null, "a method that throws yields null instead of propagating");
        MethodInfo one = Refl.Method(typeof(Methods), "One", 1);
        Assert(Refl.Invoke(one, null) == null, "an instance call with no instance yields null");
    }
    static bool Throws(Action a) {
        try {
            a();
            return false;
        } catch(Exception) {
            return true;
        }
    }
#pragma warning disable CS0414
    class ReadBase {
        private string BasePrivate = "base-private";
        public string BasePublic = "base-public";
    }
#pragma warning restore CS0414
    class ReadDerived : ReadBase {
        public int Number = 7;
        public string Prop => "prop";
        public string this[int i] => "indexer";
    }
    public static void TestTryReadFindsFieldsAndPropertiesByRuntimeType() {
        object target = new ReadDerived();
        Assert(Refl.TryRead(target, "Number", out object number) && number is 7,
            "a public field on the runtime type is readable");
        Assert(Refl.TryRead(target, "Prop", out object prop) && (string)prop == "prop",
            "a readable property is readable");
        Assert(Refl.TryRead(target, "BasePublic", out object basePublic) && (string)basePublic == "base-public",
            "an inherited public field is readable");
        Assert(Refl.TryRead(target, "BasePrivate", out object basePrivate) && (string)basePrivate == "base-private",
            "a private field on a base type is reachable through the walk");
    }
    public static void TestTryReadIsInertOnMissesAndNulls() {
        Assert(!Refl.TryRead(null, "Number", out _), "a null target reads nothing");
        Assert(!Refl.TryRead(new ReadDerived(), null, out _), "a null name reads nothing");
        Assert(!Refl.TryRead(new ReadDerived(), "", out _), "an empty name reads nothing");
        Assert(!Refl.TryRead(new ReadDerived(), "nothingNamedThis", out object missing) && missing == null,
            "an absent member reads nothing and yields null");
    }
    public static void TestTryReadNeverResolvesAnIndexer() {
        Assert(!Refl.TryRead(new ReadDerived(), "Item", out _),
            "the compiler-generated indexer must never resolve as a member");
    }
    public static void TestTryReadCachesHitsAndMisses() {
        object target = new ReadDerived();
        Refl.TryRead(target, "Number", out _);
        Refl.TryRead(target, "nothingNamedThis", out _);
        long hit = Alloc.BytesPerOp(() => Alloc.Consume(Refl.TryRead(target, "nothingNamedThis", out _)));
        Assert(hit == 0, "a cached miss must not re-run reflection or allocate, measured " + hit + " B/op");
    }
}

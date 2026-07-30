using Quartz.Compat.Game;
using static Asserts;
static class AllocationTests {
    sealed class Owner {
        public float Ratio = 0.97f;
        public int Count = 12;
        public bool Flag = true;
        public string Name = "owner";
        public float RatioProp => Ratio;
        public int Hits(HitKind kind) => (int)kind + Count;
    }
    enum HitKind { Perfect, Early, Late }
    public static void ReportBaseline() {
        Owner owner = new();
        Refl.Member ratioField = new(typeof(Owner), nameof(Owner.Ratio));
        Refl.Member ratioProp = new(typeof(Owner), nameof(Owner.RatioProp));
        Refl.Member countField = new(typeof(Owner), nameof(Owner.Count));
        Refl.Member flagField = new(typeof(Owner), nameof(Owner.Flag));
        Refl.Member nameField = new(typeof(Owner), nameof(Owner.Name));
        System.Reflection.MethodInfo hits = Refl.Method(typeof(Owner), nameof(Owner.Hits), 1);
        Console.WriteLine("  Refl.Member.Get<float>  (field)    " + Alloc.BytesPerOp(() => Alloc.Consume(ratioField.Get<float>(owner))) + " B/op");
        Console.WriteLine("  Refl.Member.Get<float>  (property) " + Alloc.BytesPerOp(() => Alloc.Consume(ratioProp.Get<float>(owner))) + " B/op");
        Console.WriteLine("  Refl.Member.Get<int>    (field)    " + Alloc.BytesPerOp(() => Alloc.Consume(countField.Get<int>(owner))) + " B/op");
        Console.WriteLine("  Refl.Member.Get<bool>   (field)    " + Alloc.BytesPerOp(() => Alloc.Consume(flagField.Get<bool>(owner))) + " B/op");
        Console.WriteLine("  Refl.Member.Get<string> (field)    " + Alloc.BytesPerOp(() => Alloc.Consume(nameField.Get<string>(owner))) + " B/op");
        Console.WriteLine("  Refl.Invoke(m, inst, enumArg)      " + Alloc.BytesPerOp(() => Alloc.Consume(Refl.Invoke(hits, owner, HitKind.Late))) + " B/op");
        Console.WriteLine("  Refl.Method cache hit              " + Alloc.BytesPerOp(() => Alloc.Consume(Refl.Method(typeof(Owner), nameof(Owner.Hits), 1))) + " B/op");
        Func<Owner, float> boundGetter = ratioProp.BindGetter<Owner, float>();
        Func<Owner, HitKind, int> boundMethod = Refl.BindMethod<Func<Owner, HitKind, int>>(hits);
        Console.WriteLine("  bound getter  -> float             " + Alloc.BytesPerOp(() => Alloc.Consume(boundGetter(owner))) + " B/op");
        Console.WriteLine("  bound method  -> int (enum arg)    " + Alloc.BytesPerOp(() => Alloc.Consume(boundMethod(owner, HitKind.Late))) + " B/op");
    }
    public static void TestABoundPropertyGetterDoesNotBox() {
        Owner owner = new();
        Refl.Member ratioProp = new(typeof(Owner), nameof(Owner.RatioProp));
        Func<Owner, float> bound = ratioProp.BindGetter<Owner, float>();
        Assert(bound != null, "a readable instance property must bind to a typed getter");
        Assert(Math.Abs(bound(owner) - owner.Ratio) < 0.0001f, "the bound getter must read the same value as the reflective path");
        long bytes = Alloc.BytesPerOp(() => Alloc.Consume(bound(owner)));
        Assert(bytes == 0, "a bound value-typed getter must not box, measured " + bytes + " B/op");
    }
    public static void TestABoundMethodDoesNotBoxItsArgumentsOrReturn() {
        Owner owner = new();
        System.Reflection.MethodInfo hits = Refl.Method(typeof(Owner), nameof(Owner.Hits), 1);
        Func<Owner, HitKind, int> bound = Refl.BindMethod<Func<Owner, HitKind, int>>(hits);
        Assert(bound != null, "a matching signature must bind to a typed delegate");
        Assert(bound(owner, HitKind.Late) == owner.Hits(HitKind.Late), "the bound delegate must return the same value as the reflective path");
        long bytes = Alloc.BytesPerOp(() => Alloc.Consume(bound(owner, HitKind.Late)));
        Assert(bytes == 0, "a bound method must not box its value arguments or its return, measured " + bytes + " B/op");
    }
    public static void TestBindingIsInertOnMismatchesRatherThanThrowing() {
        Refl.Member ratioProp = new(typeof(Owner), nameof(Owner.RatioProp));
        Assert(ratioProp.BindGetter<Owner, int>() == null, "a wrong value type must bind to null, not throw");
        Refl.Member field = new(typeof(Owner), nameof(Owner.Ratio));
        Assert(field.BindGetter<Owner, float>() == null, "a field has no getter method and must bind to null");
        Refl.Member missing = new(typeof(Owner), "nothingNamedThis");
        Assert(missing.BindGetter<Owner, float>() == null, "an absent member must bind to null");
        Assert(Refl.BindMethod<Func<Owner, int>>(null) == null, "a null MethodInfo must bind to null");
    }
    public static void TestReferenceTypedReadsAreAllocationFree() {
        Owner owner = new();
        Refl.Member nameField = new(typeof(Owner), nameof(Owner.Name));
        long bytes = Alloc.BytesPerOp(() => Alloc.Consume(nameField.Get<string>(owner)));
        Assert(bytes == 0, "a reference-typed late-bound read must not allocate, measured " + bytes + " B/op");
    }
    public static void TestMethodLookupOnAWarmCacheIsAllocationFree() {
        long bytes = Alloc.BytesPerOp(() => Alloc.Consume(Refl.Method(typeof(Owner), nameof(Owner.Hits), 1)));
        Assert(bytes == 0, "a warm Refl.Method cache hit must not allocate, measured " + bytes + " B/op");
    }
}

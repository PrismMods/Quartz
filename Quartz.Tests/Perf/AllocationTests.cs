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

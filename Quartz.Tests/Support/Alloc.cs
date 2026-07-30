using System.Runtime.CompilerServices;
static class Alloc {
    private static class Sink<T> { internal static T Value; }
    public static long BytesPerOp(Action body, int iterations = 4096) =>
        BytesFor(body, iterations) / iterations;
    public static long BytesFor(Action body, int iterations = 4096) {
        if(body == null) throw new ArgumentNullException(nameof(body));
        if(iterations < 1) throw new ArgumentOutOfRangeException(nameof(iterations));
        Warm(body);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for(int i = 0; i < iterations; i++) body();
        long after = GC.GetAllocatedBytesForCurrentThread();
        return after - before;
    }
    private static void Warm(Action body) {
        for(int i = 0; i < 64; i++) body();
        long a = GC.GetAllocatedBytesForCurrentThread();
        long b = GC.GetAllocatedBytesForCurrentThread();
        if(a != b) throw new InvalidOperationException("allocation counter is not stable on this runtime; measurements would be noise");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Consume<T>(T value) => Sink<T>.Value = value;
    public static void SelfTest() {
        long viaSink = BytesPerOp(static () => Consume(1.5f));
        if(viaSink != 0)
            throw new InvalidOperationException("Alloc.Consume must not allocate for a value type, measured " + viaSink + " B/op");
        long viaBox = BytesPerOp(static () => Consume<object>(1.5f));
        if(viaBox <= 0)
            throw new InvalidOperationException("the harness failed to see a known boxing allocation, measured " + viaBox + " B/op");
    }
}

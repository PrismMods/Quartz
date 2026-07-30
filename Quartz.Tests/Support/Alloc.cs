using System.Runtime.CompilerServices;
static class Alloc {
    private static object sink;
    public static object Sink { get => sink; set => sink = value; }
    public static long BytesPerOp(Action body, int iterations = 4096) {
        long total = BytesFor(body, iterations);
        return total / iterations;
    }
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
    public static void Consume<T>(T value) => sink = value;
}

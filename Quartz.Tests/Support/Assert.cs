using System.Diagnostics.CodeAnalysis;
static class Asserts {
    public static void Assert([DoesNotReturnIf(false)] bool condition, string message) {
        if(!condition) throw new InvalidOperationException(message);
    }
}

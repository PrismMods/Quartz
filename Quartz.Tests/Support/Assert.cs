static class Asserts {
    public static void Assert(bool condition, string message) {
        if(!condition) throw new InvalidOperationException(message);
    }
}

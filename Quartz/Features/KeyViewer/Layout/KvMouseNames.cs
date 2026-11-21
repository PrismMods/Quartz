namespace Quartz.Features.KeyViewer.Layout;
// DM Note numbers mouse buttons from 1 (MOUSE1 = left); Unity numbers them from 0 (Mouse0 = left).
// Export and import both go through this table so a round trip keeps every button.
internal static class KvMouseNames {
    internal const int Count = 7;
    private static readonly string[] names = ["MOUSE1", "MOUSE2", "MOUSE3", "MOUSE4", "MOUSE5", "MOUSE6", "MOUSE7"];
    internal static string Name(int button) => button >= 0 && button < Count ? names[button] : "";
    // Ordinal on purpose: a mixed-case "Mouse5" is Unity's own enum name and parses as such.
    internal static bool TryButton(string name, out int button) {
        button = name == null ? -1 : System.Array.IndexOf(names, name);
        return button >= 0;
    }
}

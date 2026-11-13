using Quartz.Features.Panels;
using static Asserts;
static class PanelBindsTests {
    private static Dictionary<string, (int Modifier, int Key)> Binds(params (string id, int key)[] binds) =>
        binds.ToDictionary(b => b.id, b => (0, b.key));

    private static void Same(Dictionary<string, (int Modifier, int Key)> got, Dictionary<string, (int Modifier, int Key)> want, string what) =>
        Assert(got.Count == want.Count && want.All(kv => got.TryGetValue(kv.Key, out var b) && b == kv.Value),
            $"{what}: {string.Join(", ", got.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value.Key))}");

    public static void TestDeletingAPanelKeepsTheOthersBindsOnTheirPanels() {
        // A B C D, B deleted: A stays, C and D move up one, B's binds go with it.
        var binds = Binds(("panel0_textshadow", 1), ("panel1_textshadow", 2), ("panel1_background", 3), ("panel2_localizestats", 4),
            ("panel3_textshadow", 5), ("combo_enabled", 6));
        Assert(PanelBinds.Moved(binds, [0, -1, 1, 2]), "deleting a panel with binds reports a change");
        Same(binds, Binds(("panel0_textshadow", 1), ("panel1_localizestats", 4), ("panel2_textshadow", 5), ("combo_enabled", 6)), "after deleting B");
        Assert(!PanelBinds.Moved(binds, [0, 1, 2, -1]), "deleting a panel without binds changes nothing");
    }

    public static void TestReorderingPanelsMovesEachPanelsBindsWithIt() {
        // A B C, A dragged to the bottom: B C A.
        var binds = Binds(("panel0_textshadow", 1), ("panel2_textshadow", 2), ("panel2_background", 3));
        Assert(PanelBinds.Moved(binds, [2, 0, 1]), "a reorder with binds reports a change");
        Same(binds, Binds(("panel2_textshadow", 1), ("panel1_textshadow", 2), ("panel1_background", 3)), "after B C A");
        Assert(!PanelBinds.Moved(binds, [0, 1, 2]), "the same order changes nothing");
        Same(binds, Binds(("panel2_textshadow", 1), ("panel1_textshadow", 2), ("panel1_background", 3)), "after the same order");
    }

    public static void TestReorderThenDeleteKeepsTheBindsOnTheirPanels() {
        // A B C with a bind on C's Text Shadow; A dragged to the bottom (B C A), then A deleted (B C).
        var binds = Binds(("panel0_textshadow", 1), ("panel1_background", 2), ("panel2_textshadow", 3));
        PanelBinds.Moved(binds, [2, 0, 1]);
        Assert(PanelBinds.Moved(binds, [0, 1, -1]), "deleting A, now last, reports a change");
        Same(binds, Binds(("panel0_background", 2), ("panel1_textshadow", 3)), "B C after the drag and the delete");
    }

    public static void TestBindsThatAreNoPanelsStayAsTheyAre() {
        // Not a panel's row ("panels_font", "panelx_y"), or a place past the list, left by a panel deleted before hotkeys
        // followed their panels: no panel owns them, so a delete or a reorder leaves them alone.
        var binds = Binds(("panels_font", 1), ("panel_textshadow", 2), ("panelx_y", 3), ("panel3_textshadow", 4), ("panel10_background", 5),
            ("panel99999999999_x", 6), ("panel1", 7));
        var want = new Dictionary<string, (int Modifier, int Key)>(binds);
        Assert(!PanelBinds.Moved(binds, [1, 0, -1]), "binds that are no panel's change nothing");
        Same(binds, want, "no panel's binds after a reorder and a delete");
    }
}

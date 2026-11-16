#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Features.KeyLimiter;
using Quartz.Game;
using Quartz.UI.Utility;
using Quartz.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.GameplayPages;
/// <summary>Gameplay ▸ Key Limiter in the new menu, row for row as PageKeyLimiter. Profile changes are structural and record
/// no undo step, as the classic page has none. The page follows KeyLimiter.Changed and KeyBindSync.Changed while it shows.</summary>
internal static class KeyLimiterPage {
    internal static void Build(VisualElement p) {
        KeyLimiter.EnsureConf();
        ChartKeyLimiter.EnsureConf();
        ShellPage.Feature(p, "keylimiter", "SECTION_KEY_LIMITER", "KEYLIMITER_DESC", null, Cards);
    }

    private static bool Locked() => KeyBindSync.IsSyncing;
    private static bool Unlocked() => !KeyBindSync.IsSyncing;

    private static void Cards(VisualElement p) {
        VisualElement card = Controls.Card(p, null);
        ShellRows.Toggle(card, "KEYVIEWER_SYNCLIMITER", "keyviewer_synclimiter", Bind.To(() => KeyBindSync.IsSyncing, KeyBindSync.SetSyncing), null, null, o => {
            o.Default = KeyBindSync.Default;
            o.Tooltip = "DESC_KEYVIEWER_SYNCLIMITER";
        });
        // Rebuilt when a profile comes or goes, so the dropdown lists them all.
        Controls.Live(card, () => KeyLimiter.Profiles.Count, box => {
            int[] indices = [.. Enumerable.Range(0, KeyLimiter.Profiles.Count)];
            Controls.Dropdown(box, "KL_PROFILE_LABEL", QuartzShell.Writes(() => KeyLimiter.ActiveProfileIndex, KeyLimiter.SwitchProfile), indices,
                ProfileName, o => {
                    o.Id = "KL_PROFILE_LABEL";
                    o.Undoable = false;
                });
        });
        ShellRows.Text(card, "KL_PROFILE_NAME", Bind.To(() => ProfileName(KeyLimiter.ActiveProfileIndex), KeyLimiter.RenameActiveProfile), 0, null, null, o => {
            o.Tooltip = "DESC_KL_PROFILE_NAME";
            o.Undoable = false;
        });
        ShellRows.Buttons(card, new ShellRows.Btn("KL_ADD_PROFILE", KeyLimiter.AddProfile, more: o => o.Undoable = false),
            new ShellRows.Btn("KL_REMOVE_PROFILE", KeyLimiter.RemoveActiveProfile, enabled: () => KeyLimiter.Profiles.Count > 1,
                more: o => o.Undoable = false));
        Button capture = ShellRows.CaptureButton(card, "KL_CAPTURE", () => KeyLimiter.IsCapturing, done => KeyLimiter.StartCapture(k => KeyLimiter.ToggleAllowedKey(k), done),
            KeyLimiter.CancelCapture, o => {
                o.Tooltip = "DESC_KL_CAPTURE";
                o.EnabledWhen = Unlocked;
            }, () => KeyLimiter.IsCapturing && setting == KeyCode.None);
        ShellRows.Inline(capture, Controls.Action(card, "", "KL_CLEAR", KeyLimiter.ClearAllowedKeys, o => {
            o.Id = "KL_CLEAR";
            o.EnabledWhen = Unlocked;
        }));
        Hint(card, "KL_SYNC_LOCKED", Locked);

        Label head = ShellRows.Header(card, "KL_ALLOWED_KEYS");
        Controls.Hook(head, o => {
            o.Id = "KL_ALLOWED_KEYS";
            o.VisibleWhen = () => Keys().Length > 0;
        });
        Controls.Repeat<int>(card, Keys, k => KeyName(k) + ":" + Array.IndexOf(Keys(), k), KeyRow);
        Hint(card, "KL_NO_ALLOWED_KEYS", () => Keys().Length == 0);

        // Subscribed only while shown: the search index builds this page detached.
        Action changed = () => QuartzShell.Surface?.Refresh();
        Action sync = () => {
            if(KeyBindSync.IsSyncing && KeyLimiter.IsCapturing) KeyLimiter.CancelCapture();
            QuartzShell.Surface?.Refresh();
        };
        Ui.WhileAttached(card, () => {
            KeyLimiter.Changed += changed;
            KeyBindSync.Changed += sync;
        }, () => {
            KeyLimiter.Changed -= changed;
            KeyBindSync.Changed -= sync;
        });

        VisualElement chart = Controls.Card(p, "KL_CHART_SECTION", o => o.Id = "KL_CHART_SECTION");
        ShellRows.Toggle(chart, "CHARTKEYLIMITER_ENABLE", "chartkeylimiter_enable", Bind.To(() => ChartKeyLimiter.Conf.Enabled, v => ChartKeyLimiter.Conf.Enabled = v),
            ChartKeyLimiter.Apply, ChartKeyLimiter.Save, o => o.Default = new ChartKeyLimiterSettings().Enabled);
        Hint(chart, "KL_CHART_NOTE", null);
    }

    private static int[] Keys() => KeyLimiter.Conf?.AllowedKeys ?? [];

    private static string KeyName(int k) => Keybind.KeyName(KeyCodes.Normalize((KeyCode)k));

    // The allowed key a row's Set is capturing for; None while no row's capture runs.
    private static KeyCode setting = KeyCode.None;

    // An allowed key's name with Set and Remove, both absent while the key viewer owns the keys. Set reads PRESS_A_KEY while
    // its own capture runs.
    private static void KeyRow(VisualElement box, int k) {
        int n = Array.IndexOf(Keys(), k) + 1;
        KeyCode key = KeyCodes.Normalize((KeyCode)k);
        Button set = Controls.Action(box, KeyName(k), "SET", () => {
            if(KeyLimiter.IsCapturing) KeyLimiter.CancelCapture();
            else {
                setting = key;
                KeyLimiter.StartCapture(nk => KeyLimiter.ReplaceAllowedKey(key, nk), () => setting = KeyCode.None);
            }
        }, o => o.Id = "SET#" + n);
        string face = "SET";
        Button remove = Ui.Btn(set.parent, "REMOVE", () => KeyLimiter.ToggleAllowedKey(key));
        remove.name = "REMOVE#" + n;
        remove.style.marginLeft = Theme.For(remove).Sizes.Gap;
        Controls.Hook(set, o => o.VisibleWhen = () => {
            string want = KeyLimiter.IsCapturing && setting == key ? "PRESS_A_KEY" : "SET";
            if(want != face) Words.Label(set, face = want);
            return Unlocked();
        });
        Controls.Hook(remove, o => o.VisibleWhen = Unlocked);
    }

    private static string ProfileName(int index) {
        IReadOnlyList<KeyLimiterProfile> profiles = KeyLimiter.Profiles;
        string name = index >= 0 && index < profiles.Count ? profiles[index].Name : null;
        return string.IsNullOrEmpty(name) ? "Profile " + (index + 1) : name;
    }

    // A muted line of the classic page, Id key.
    private static void Hint(VisualElement parent, string key, Func<bool> visible) => Controls.Info(parent, key, () => "", (InfoOptions o) => {
        o.Id = key;
        o.VisibleWhen = visible;
    });
}
#endif

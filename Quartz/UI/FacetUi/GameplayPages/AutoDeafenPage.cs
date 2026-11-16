#if !IL2CPP
using Facet;
using Quartz.Core;
using Quartz.Features.AutoDeafen;
using Quartz.Game;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi.GameplayPages;
/// <summary>Gameplay ▸ Auto Deafen (Discord) in the new menu, row for row as PageAutoDeafen. Each mode's rows sit in a region
/// that shows while that mode is the effective one, so a mode change needs no rebuild. The status line is read every 0.25 s
/// while the page shows.</summary>
internal static class AutoDeafenPage {
    private static AutoDeafenSettings C => AutoDeafen.Conf;
    private static readonly AutoDeafenSettings Def = new();

    internal static void Build(VisualElement p) {
        AutoDeafen.EnsureConf();
        ShellPage.Feature(p, "autodeafen", "SECTION_AUTO_DEAFEN_DISCORD", "AUTODEAFEN_DESC", null, Cards);
    }

    private static void Cards(VisualElement p) {
        Action save = AutoDeafen.Save;
        VisualElement card = Controls.Card(p, null);
        var mode = Controls.Dropdown(card, "AD_MODE_LABEL", QuartzShell.Writes(() => AutoDeafen.EffectiveMode, v => {
            C.Mode = v;
            save();
        }), PageRules.DeafenModes(AutoDeafen.ShortcutSupported, VoiceDeafen.Registered), m => m switch {
            AutoDeafenSettings.ModeShortcut => "AD_MODE_SHORTCUT",
            AutoDeafenSettings.ModeDiscord => "AD_MODE_DISCORD",
            _ => "AD_MODE_BOT",
        }, o => {
            o.Id = "AD_MODE_LABEL";
            o.Default = AutoDeafen.ShortcutSupported ? AutoDeafenSettings.ModeShortcut : AutoDeafenSettings.ModeBot;
        });
        // The Discord mode's note is the mode row's info icon, shown in that mode only.
        ShellRows.Describe(mode.Root, "AD_DISCORD_HINT", () => AutoDeafen.EffectiveMode == AutoDeafenSettings.ModeDiscord);
        Hint(Controls.When(card, () => !AutoDeafen.ShortcutSupported), "AD_SHORTCUT_WINDOWS_ONLY");
        ShellRows.ReleaseSlider(card, "AD_PCT", Bind.To(() => C.DeafenAtPercent, v => C.DeafenAtPercent = v), 0f, 100f, 1f, "0", null, save,
            o => o.Default = Def.DeafenAtPercent);
        ShellRows.Toggle(card, "AD_START", "ad_start", Bind.To(() => C.OnlyFromStart, v => C.OnlyFromStart = v), null, save,
            o => o.Default = Def.OnlyFromStart);
        ShellRows.Toggle(card, "AD_SKIP_AUTO", "ad_skip_auto", Bind.To(() => C.SkipWhenAuto, v => C.SkipWhenAuto = v), null, save,
            o => o.Default = Def.SkipWhenAuto);

        VisualElement shortcut = Controls.When(card, () => AutoDeafen.EffectiveMode == AutoDeafenSettings.ModeShortcut);
        ShellRows.Toggle(shortcut, "AD_KB_CTRL", "ad_kb_ctrl", Bind.To(() => C.ShortcutCtrl, v => C.ShortcutCtrl = v), null, save,
            o => o.Default = Def.ShortcutCtrl);
        ShellRows.Toggle(shortcut, "AD_KB_SHIFT", "ad_kb_shift", Bind.To(() => C.ShortcutShift, v => C.ShortcutShift = v), null, save,
            o => o.Default = Def.ShortcutShift);
        ShellRows.Toggle(shortcut, "AD_KB_ALT", "ad_kb_alt", Bind.To(() => C.ShortcutAlt, v => C.ShortcutAlt = v), null, save,
            o => o.Default = Def.ShortcutAlt);
        ShellRows.Toggle(shortcut, "AD_KB_META", "ad_kb_meta", Bind.To(() => C.ShortcutMeta, v => C.ShortcutMeta = v), null, save,
            o => o.Default = Def.ShortcutMeta);
        // As the classic capture: no mouse or joystick button, no modifier; Escape cancels.
        Control<string> keyRow = ShellRows.Key(shortcut, "AD_KEY", () => (KeyCode)C.ShortcutKey, k => C.ShortcutKey = (int)k, save,
            accept: k => k != KeyCode.None && (int)k < (int)KeyCode.Mouse0 && !Keybind.IsModifier(k));
        ShellRows.Describe(keyRow.Root, "AD_SHORTCUT_HINT");


        VisualElement bot = Controls.When(card, () => AutoDeafen.EffectiveMode == AutoDeafenSettings.ModeBot);
        ShellRows.Text(bot, "AD_CLIENT_ID", Bind.To(() => C.DiscordClientId, v => C.DiscordClientId = v), 0, null, save,
            o => o.Default = Def.DiscordClientId);
        ShellRows.Buttons(bot, new("AD_AUTHORIZE", AutoDeafen.OpenAuthorizeUrl), new("AD_COPY_URL", AutoDeafen.CopyAuthorizeUrl),
            new("AD_TUTORIAL", AutoDeafen.OpenTutorial), new("AD_UNLINK", AutoDeafen.Unlink));

        Control<string> status = ShellRows.Info(card, "AD_STATUS", () => AutoDeafen.Status,
            s => MainCore.Tr.Get("STATUS_PREFIX", "Status: ") + s);
        Ui.Every(status.Root, 250, () => QuartzShell.Surface?.Refresh());
    }

    // A muted line of the classic page, Id key.
    private static void Hint(VisualElement parent, string key) => Controls.Info(parent, key, () => "", (InfoOptions o) => o.Id = key);
}
#endif

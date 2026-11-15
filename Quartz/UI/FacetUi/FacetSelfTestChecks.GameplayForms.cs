#if DEBUG && !IL2CPP
using System.Collections;
using Facet;
using Quartz.Core;
using Quartz.Features.AutoDeafen;
using Quartz.Features.Countdown;
using Quartz.Features.DeathLimit;
using Quartz.Features.KeyLimiter;
using Quartz.Game;
using Quartz.UI.Utility;
using UnityEngine;
using UnityEngine.UIElements;
namespace Quartz.UI.FacetUi;
/// <summary>
/// The Gameplay form pages in the new menu, each against its classic page: rows, search hits, Korean text and a write
/// reaching its file, then each page's own rows (the tempo range, the limit switches, the deafen modes, status and key).
/// </summary>
internal static partial class FacetSelfTestChecks {
    private static IEnumerator GameplayForms(Action<string, bool, string> check) {
        yield return GameplayChatter(check);
        yield return GameplayCountdown(check);
        yield return GameplayDeathLimit(check);
        yield return GameplayAutoDeafen(check);
    }

    private static IEnumerator GameplayChatter(Action<string, bool, string> check) {
        const string page = "gameplay.chatter";
        ChatterBlocker.EnsureConf();
        string[] keys = ["CHATTERBLOCKER_ENABLE", "KCB_MS"];
        yield return FeatureFormPage(check, "keylimiter", page, ChatterBlocker.ConfMgr, null, null, keys, keys, () => More());
        IEnumerator More() {
            float want = Away(ChatterBlocker.Conf.ThresholdMs);
            yield return Saved(check, page, "KCB_MS", () => SlideTo("KCB_MS", 0f, 100f, want),
                () => Mathf.Abs(ChatterBlocker.Conf.ThresholdMs - want) < 0.5f, ChatterBlocker.ConfMgr.Path);
        }
    }

    private static IEnumerator GameplayCountdown(Action<string, bool, string> check) {
        const string page = "gameplay.countdown";
        CountdownFeature.EnsureConf();
        CountdownSettings C() => CountdownFeature.Conf;
        string[] keys = ["COUNTDOWN_MINBPM", "COUNTDOWN_MAXBPM"];
        yield return FeatureFormPage(check, "countdown", page, CountdownFeature.ConfMgr, () => (C().MinBpm, C().MaxBpm) = (400f, 600f), null,
            keys, keys, () => More());
        IEnumerator More() {
            TextField field = PageContent().Q<TextField>("COUNTDOWN_MINBPM-input");
            if(field != null) {
                field.value = "700";
                using KeyDownEvent ev = Pooled<KeyDownEvent>();   // Enter commits it
                typeof(KeyboardEventBase<KeyDownEvent>).GetProperty("keyCode").SetValue(ev, KeyCode.Return);
                ev.target = field;
                field.SendEvent(ev);
            }
            yield return Frames(2);
            (float lo, float hi) typed = (C().MinBpm, C().MaxBpm);
            bool undid = global::Facet.History.Undo();
            yield return Frames(2);
            check($"{page}: a low tempo typed above the high pushes it, and undo puts both back",
                field != null && typed == (700f, 700f) && undid && (C().MinBpm, C().MaxBpm) == (400f, 600f),
                $"field={field != null} typed={typed.lo}/{typed.hi} undo={undid} now={C().MinBpm}/{C().MaxBpm}");
            yield return HeaderHotkeyCheck(check, page, "SECTION_COUNTDOWN", "countdown");
        }
    }

    private static IEnumerator GameplayDeathLimit(Action<string, bool, string> check) {
        const string page = "gameplay.death";
        DeathLimit.EnsureConf();
        DeathLimitSettings C() => DeathLimit.Conf;
        string[] shown = ["DL_DEATHS_ON", "DL_DEATHS_MAX", "DL_MISSES_ON", "DL_OVERLOADS_ON", "DL_OVERLOADS_MAX", "DL_MESSAGE"];
        yield return FeatureFormPage(check, "deathlimit", page, DeathLimit.ConfMgr, () => {
            C().MaxDeathsOn = C().MaxOverloadsOn = true;
            C().MaxMissesOn = false;
        }, null, shown, [.. shown, "DL_MISSES_MAX"], () => More(), ["DL_MISSES_MAX"]);
        IEnumerator More() {
            // The misses switch turned on here shows its slider.
            yield return Saved(check, page, "DL_MISSES_ON", () => ClickRow("DL_MISSES_ON"), () => C().MaxMissesOn, DeathLimit.ConfMgr.Path);
            yield return Parity(check, page, ["DL_MISSES_MAX"]);
            yield return Hotkey(check, page, "DL_MISSES_ON", "dl_misses_on", null, Bind.To(() => C().MaxMissesOn, v => {
                C().MaxMissesOn = v;
                DeathLimit.Save();
            }));
        }
    }

    private static IEnumerator GameplayAutoDeafen(Action<string, bool, string> check) {
        const string page = "gameplay.autodeafen";
        AutoDeafen.EnsureConf();
        AutoDeafenSettings C() => AutoDeafen.Conf;
        bool sc = AutoDeafen.ShortcutSupported, ours = false;
        string[] bot = ["AD_CLIENT_ID", "AD_AUTHORIZE", "AD_COPY_URL", "AD_TUTORIAL", "AD_UNLINK"];
        string[] rows = ["AD_MODE_LABEL", "AD_PCT", "AD_START", "AD_SKIP_AUTO", .. bot];
        string[] shortcut = ["AD_SHORTCUT_HINT", "AD_KB_CTRL", "AD_KB_SHIFT", "AD_KB_ALT", "AD_KB_META", "AD_KEY"];
        string[] keys = sc ? rows : [.. rows, "AD_SHORTCUT_WINDOWS_ONLY"];
        string[] hidden = [.. shortcut, "AD_DISCORD_HINT", .. sc ? ["AD_SHORTCUT_WINDOWS_ONLY"] : Array.Empty<string>()];
        try {
            yield return FeatureFormPage(check, "autodeafen", page, AutoDeafen.ConfMgr, () => {
                C().Mode = AutoDeafenSettings.ModeBot;
                // With no mod giving a voice source the run gives its own, before the page is built, so the mode dropdown
                // lists Discord on every platform; its status text is the run's to move.
                if(!VoiceDeafen.Registered) {
                    deafenVoice = "self-test";
                    VoiceDeafen.Register(DeafenVoiceId, () => deafenVoice, _ => { });
                    ours = true;
                }
            }, null, keys, rows, () => More(), hidden);
        } finally {
            if(ours) VoiceDeafen.Unregister(DeafenVoiceId);
        }
        IEnumerator More() {
            float want = Away(C().DeafenAtPercent);
            yield return Saved(check, page, "AD_PCT", () => SlideTo("AD_PCT", 0f, 100f, want),
                () => Mathf.Abs(C().DeafenAtPercent - want) < 0.5f, AutoDeafen.ConfMgr.Path);
            yield return DeafenMode(check, page, bot, shortcut, ours);
            yield return DeafenKeyLeave(check, page);
        }
    }

    private const string DeafenVoiceId = "quartz-selftest";
    private static string deafenVoice;

    // Discord picked in the mode dropdown saves and swaps the mode regions in place, and bot picked swaps them back. In Discord
    // mode the status line reads the voice source's status within 0.5 s of it changing. A mode other than bot stops the
    // feature's Discord link on its next tick, so with one live (or about to start from a stored token) the run leaves the mode
    // alone.
    private static IEnumerator DeafenMode(Action<string, bool, string> check, string page, string[] bot, string[] shortcut, bool ours) {
        AutoDeafenSettings C() => AutoDeafen.Conf;
        if(DiscordOAuthServer.Running || C().Enabled && !string.IsNullOrWhiteSpace(C().DiscordAccessToken)) {
            formInfo($"{page} AD_MODE_LABEL: a mode picked in the dropdown and the status line: not run, a Discord link is live");
            yield break;
        }
        string path = AutoDeafen.ConfMgr.Path;
        byte[] before = Bytes(path);
        yield return PickItem("AD_MODE_LABEL", QuartzHost.Host.Translate("AD_MODE_DISCORD"));
        bool saved = false;
        for(float t0 = Time.realtimeSinceStartup; !saved && Time.realtimeSinceStartup - t0 < 2f;) {
            yield return null;
            saved = C().IsDiscord && !SameBytes(Bytes(path), before);
        }
        check($"{page} AD_MODE_LABEL: Discord picked in the dropdown reaches the setting and its file", saved,
            $"mode={C().Mode} effective={AutoDeafen.EffectiveMode} ({path})");
        yield return Parity(check, page, ["AD_DISCORD_HINT"], [.. bot, .. shortcut]);

        if(ours) {
            VisualElement row = PageContent().Q(name: "AD_STATUS");
            string want = null;
            bool seen = false;
            deafenVoice = "self-test moved";
            for(float t0 = Time.realtimeSinceStartup; !seen && Time.realtimeSinceStartup - t0 < 0.5f;) {
                yield return null;
                want = MainCore.Tr.Get("STATUS_PREFIX", "Status: ") + AutoDeafen.Status;
                seen = AutoDeafen.Status.EndsWith(deafenVoice) && row != null && row.Query<Label>().ToList().Any(l => l.text == want);
            }
            check($"{page}: the status row follows AutoDeafen.Status within 0.5 s", seen, $"row={row != null} want='{want}'");
        } else {
            formInfo($"{page}: the status row following AutoDeafen.Status: not run, another mod's voice source is registered");
        }

        yield return PickItem("AD_MODE_LABEL", QuartzHost.Host.Translate("AD_MODE_BOT"));
        check($"{page} AD_MODE_LABEL: bot picked in the dropdown reaches the setting", C().Mode == AutoDeafenSettings.ModeBot, $"mode={C().Mode}");
        yield return Parity(check, page, bot, [.. shortcut, "AD_DISCORD_HINT"]);
    }

    // A capture begun on the Discord key row ends when the page is left. Shortcut mode only exists where a shortcut can be sent.
    private static IEnumerator DeafenKeyLeave(Action<string, bool, string> check, string page) {
        if(!AutoDeafen.ShortcutSupported) {
            formInfo($"{page} AD_KEY: leaving the page during a capture: not run, shortcut mode is Windows only");
            yield break;
        }
        AutoDeafen.Conf.Mode = AutoDeafenSettings.ModeShortcut;   // the page's restore puts the mode back
        QuartzShell.Surface?.Refresh();
        yield return Frames(2);
        Control<string> row = KeyRow(PageContent().Q(name: "AD_KEY-input"));
        bool began = row != null && row.BeginCapture();
        yield return Frames(1);
        bool capturing = KeyCaptureService.IsCapturing;
        QuartzShell.Go("profiles.main");
        yield return Frames(2);
        check($"{page} AD_KEY: leaving the page during a capture ends it", began && capturing && !KeyCaptureService.IsCapturing,
            $"row={row != null} began={began} capturing={capturing} after={KeyCaptureService.IsCapturing}");
        KeyCaptureService.Cancel();
    }
}
#endif

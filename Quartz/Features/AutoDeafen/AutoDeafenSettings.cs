using System;
using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.Features.AutoDeafen;
public sealed class AutoDeafenSettings : ISettingsFile {
    public bool Enabled = false;
    public float DeafenAtPercent = 5f;
    public bool OnlyFromStart = true;
    public bool SkipWhenAuto = true;
    public const string ModeShortcut = "shortcut";
    public const string ModeBot = "bot";
    public string Mode = ModeShortcut;
    public bool IsShortcut => string.Equals(Mode, ModeShortcut, StringComparison.OrdinalIgnoreCase);
    public bool ShortcutCtrl = true;
    public bool ShortcutShift = true;
    public bool ShortcutAlt = false;
    public bool ShortcutMeta = false;
    public int ShortcutKey = (int)KeyCode.D;
    public string DiscordClientId = "";
    // Runtime-only. OAuth bearer tokens are persisted separately from profile JSON
    // so exporting or switching a profile cannot disclose or replace credentials.
    public string DiscordAccessToken = "";
    internal string LegacyDiscordAccessToken = "";
    public JToken Serialize() {
        return new JObject {
            [nameof(Enabled)] = Enabled,
            [nameof(DeafenAtPercent)] = DeafenAtPercent,
            [nameof(OnlyFromStart)] = OnlyFromStart,
            [nameof(SkipWhenAuto)] = SkipWhenAuto,
            [nameof(Mode)] = Mode,
            [nameof(ShortcutCtrl)] = ShortcutCtrl,
            [nameof(ShortcutShift)] = ShortcutShift,
            [nameof(ShortcutAlt)] = ShortcutAlt,
            [nameof(ShortcutMeta)] = ShortcutMeta,
            [nameof(ShortcutKey)] = ShortcutKey,
            [nameof(DiscordClientId)] = DiscordClientId,
        };
    }
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        DeafenAtPercent = IOUtils.Read(token, nameof(DeafenAtPercent), DeafenAtPercent);
        OnlyFromStart = IOUtils.Read(token, nameof(OnlyFromStart), OnlyFromStart);
        SkipWhenAuto = IOUtils.Read(token, nameof(SkipWhenAuto), SkipWhenAuto);
        Mode = IOUtils.Read(token, nameof(Mode), Mode);
        ShortcutCtrl = IOUtils.Read(token, nameof(ShortcutCtrl), ShortcutCtrl);
        ShortcutShift = IOUtils.Read(token, nameof(ShortcutShift), ShortcutShift);
        ShortcutAlt = IOUtils.Read(token, nameof(ShortcutAlt), ShortcutAlt);
        ShortcutMeta = IOUtils.Read(token, nameof(ShortcutMeta), ShortcutMeta);
        ShortcutKey = IOUtils.Read(token, nameof(ShortcutKey), ShortcutKey);
        DiscordClientId = IOUtils.Read(token, nameof(DiscordClientId), DiscordClientId);
        LegacyDiscordAccessToken = IOUtils.Read(token, nameof(DiscordAccessToken), "");
    }
}

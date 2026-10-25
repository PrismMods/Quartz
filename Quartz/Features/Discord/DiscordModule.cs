using Quartz.Game;
using Quartz.UI.Nav;
using Quartz.Features.Discord.Api;
namespace Quartz.Features.Discord;
[Feature("discord", "others", 10, "Discord", "SECTION_DISCORD")]
public sealed class DiscordModule : QuartzFeature {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(DiscordModule), $"Quartz.Features.Discord.Lang.{lang}.json");
        Context.AddPage(new NavPage {
            Key = "discord.main",
            CategoryKey = "discord",
            Order = 10,
            Title = "Discord",
            LocaleKey = "SECTION_DISCORD",
            Build = PageDiscord.Create,
            OwnScroll = true,
        });
        VoiceDeafen.Register("discord", VoiceSession.DeafenStatus, VoiceSession.SetDeaf);
    }
    public override void OnUnload() {
        VoiceSession.SetDeaf(false);
        VoiceDeafen.Unregister("discord");
    }
}

using Quartz.Game;
using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Discord;
public sealed class DiscordModule : QuartzModule {
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

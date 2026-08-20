using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Minecraft;
public sealed class MinecraftModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(MinecraftModule), $"Quartz.Features.Minecraft.Lang.{lang}.json");
        McAssemblies.EnsureLoaded();
        Context.AddPage(new NavPage {
            Key = "minecraft.main",
            CategoryKey = "minecraft",
            Order = 20,
            Title = "Minecraft",
            LocaleKey = "SECTION_MINECRAFT",
            Build = PageMinecraft.Create,
            OwnScroll = true,
        });
    }
    public override void OnUnload() { }
}

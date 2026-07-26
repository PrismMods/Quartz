using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Nostalgia;
public sealed class NostalgiaTweaksModule : QuartzModule {
    public override void OnLoad() {
        Nostalgia.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "nostalgia.tweaks",
            CategoryKey = "nostalgia",
            Order = 30,
            Title = "Tweaks",
            LocaleKey = "TWEAKS",
            SearchLabel = static () => Quartz.Core.MainCore.Tr.Get("NOSTALGIA", "Nostalgia"),
            OwnScroll = true,
            Build = NostalgiaUI.TweaksPage,
        });
        Context.PatchAll(typeof(NostalgiaTweaksModule));
        Context.OnModEnable("NostalgiaTweaks", Nostalgia.Refresh);
        Context.OnModDisable("NostalgiaTweaks", Nostalgia.Refresh);
    }
}

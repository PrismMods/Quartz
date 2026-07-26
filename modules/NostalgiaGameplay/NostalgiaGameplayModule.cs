using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Nostalgia;
public sealed class NostalgiaGameplayModule : QuartzModule {
    public override void OnLoad() {
        Nostalgia.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "nostalgia.gameplay",
            CategoryKey = "nostalgia",
            Order = 10,
            Title = "Gameplay",
            LocaleKey = "GAMEPLAY",
            SearchLabel = static () => Quartz.Core.MainCore.Tr.Get("NOSTALGIA", "Nostalgia"),
            OwnScroll = true,
            Build = NostalgiaUI.GameplayPage,
        });
        Context.PatchAll(typeof(NostalgiaGameplayModule));
        Context.OnModEnable("NostalgiaGameplay", Nostalgia.Refresh);
        Context.OnModDisable("NostalgiaGameplay", Nostalgia.Refresh);
    }
}

using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Nostalgia;
public sealed class NostalgiaVisualsModule : QuartzModule {
    public override void OnLoad() {
        Nostalgia.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "nostalgia.visuals",
            CategoryKey = "nostalgia",
            Order = 20,
            Title = "Visuals",
            LocaleKey = "VISUALS",
            SearchLabel = static () => Quartz.Core.MainCore.Tr.Get("NOSTALGIA", "Nostalgia"),
            OwnScroll = true,
            Build = NostalgiaUI.VisualsPage,
        });
        Context.PatchAll(typeof(NostalgiaVisualsModule));
        Context.OnModEnable("NostalgiaVisuals", Nostalgia.Refresh);
        Context.OnModDisable("NostalgiaVisuals", Nostalgia.Refresh);
    }
}

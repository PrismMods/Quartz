using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Nostalgia;
public sealed class NostalgiaEditorModule : QuartzModule {
    public override void OnLoad() {
        Nostalgia.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "nostalgia.editor",
            CategoryKey = "nostalgia",
            Order = 40,
            Title = "Editor",
            LocaleKey = "EDITOR",
            SearchLabel = static () => Quartz.Core.MainCore.Tr.Get("NOSTALGIA", "Nostalgia"),
            OwnScroll = true,
            Build = NostalgiaUI.EditorPage,
        });
        Context.PatchAll(typeof(NostalgiaEditorModule));
        Context.OnModEnable("NostalgiaEditor", Nostalgia.Refresh);
        Context.OnModDisable("NostalgiaEditor", Nostalgia.Refresh);
    }
}

using Quartz.Modules;
using Quartz.Resource;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Nostalgia;
public sealed class NostalgiaModule : QuartzModule {
    internal static ResourceManager Resources { get; private set; }
    public override void OnLoad() {
        Resources = Context.Resources(typeof(NostalgiaModule), "Quartz.Features.Nostalgia.Resource.");
        Context.RegisterTranslations(typeof(NostalgiaModule), "Quartz.Features.Nostalgia.Lang.en-US.json");
        Context.RegisterTranslations(typeof(NostalgiaModule), "Quartz.Features.Nostalgia.Lang.ko-KR.json");
        Context.RegisterTranslations(typeof(NostalgiaModule), "Quartz.Features.Nostalgia.Lang.zh-CN.json");
        Nostalgia.EnsureConf();
        Context.PatchAll(typeof(NostalgiaModule));
        Context.OnModEnable("Nostalgia", Nostalgia.Refresh);
        Context.OnModDisable("Nostalgia", Nostalgia.Restore);
    }
    public override void OnUnload() {
        Nostalgia.Restore();
        Resources = null;
    }
}

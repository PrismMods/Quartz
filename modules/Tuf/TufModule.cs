using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Generator;
using Quartz.UI.Nav;
namespace Quartz.Features.Tuf;
public sealed class TufModule : QuartzModule {
    public const string LevelsPageKey = "tuf.levels";
    private TufService tufService;
    private TufPackService packService;
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(TufModule), $"Quartz.Features.Tuf.Lang.{lang}.json");
        tufService = new TufService();
        packService = new TufPackService();
        tufService.Initialize();
        packService.Initialize();
        Context.AddPage(new NavPage {
            Key = LevelsPageKey,
            CategoryKey = "tuf",
            Order = 10,
            Title = "Levels",
            LocaleKey = "TUF_LEVELS",
            Build = TufBrowserUI.Create,
            SearchLabel = static () => GenerateUI.Tr("TUF", "TUF"),
            OwnScroll = true,
        });
        Context.AddPage(new NavPage {
            Key = "tuf.packs",
            CategoryKey = "tuf",
            Order = 20,
            Title = "Packs",
            LocaleKey = "TUF_PACKS",
            Build = TufPacksUI.Create,
            SearchLabel = static () => GenerateUI.Tr("TUF", "TUF") + " · " + GenerateUI.Tr("TUF_PACKS", "Packs"),
            OwnScroll = true,
        });
        Context.AddPage(new NavPage {
            Key = "tuf.settings",
            CategoryKey = "tuf",
            Order = 30,
            Title = "Settings",
            LocaleKey = "TUF_SETTINGS",
            Build = TufSettingsUI.Create,
            SearchLabel = static () => GenerateUI.Tr("TUF", "TUF") + " · " + GenerateUI.Tr("TUF_SETTINGS", "Settings"),
            OwnScroll = true,
        });
        Context.AddHomeCard(new Quartz.UI.Home.HomeCard {
            Key = "tuf.home",
            Title = "TUF",
            LocaleKey = "TUF",
            Order = 30,
            Build = body => {
                TufService service = TufService.Instance;
                if(service == null) return;
                Quartz.UI.Home.HomeUI.Line(body, string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    Quartz.Core.MainCore.Tr.Get("TUF_HOME_INSTALLED", "{0} levels installed"),
                    service.InstalledCount));
                Quartz.UI.Home.HomeUI.Line(body, service.ActiveRootPath);
            },
        });
        Context.PatchAll(typeof(TufModule));
    }
    public override void OnUnload() {
        packService?.Dispose();
        tufService?.Dispose();
        packService = null;
        tufService = null;
    }
}

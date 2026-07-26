using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.KeyLimiter;
public sealed class KeyLimiterModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(KeyLimiterModule), $"Quartz.Features.KeyLimiter.Lang.{lang}.json");
        KeyLimiter.EnsureConf();
        ChatterBlocker.ChatterBlocker.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "gameplay.keylimiter",
            CategoryKey = "gameplay",
            Order = 10,
            Title = "Key Limiter",
            LocaleKey = "SECTION_KEY_LIMITER",
            Build = PageKeyLimiter.KeyLimiterPage,
            OwnScroll = true,
        });
        Context.AddPage(new NavPage {
            Key = "gameplay.chatter",
            CategoryKey = "gameplay",
            Order = 20,
            Title = "Keyboard Chatter Blocker",
            LocaleKey = "SECTION_KEYBOARD_CHATTER_BLOCKER",
            Build = PageKeyLimiter.ChatterBlockerPage,
            OwnScroll = true,
        });
        Context.AddHomeCard(new Quartz.UI.Home.HomeCard {
            Key = "keylimiter.home",
            Title = "Key Limiter",
            LocaleKey = "SECTION_KEY_LIMITER",
            Order = 20,
            Build = body => {
                KeyLimiter.EnsureConf();
                KeyLimiterSettings conf = KeyLimiter.Conf;
                if(conf == null) return;
                Quartz.UI.Home.HomeUI.Line(body, string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    Quartz.Core.MainCore.Tr.Get("KL_HOME_KEYS", "{0}: {1} keys · {2}"),
                    conf.ActiveProfileOrDefault().Name,
                    conf.AllowedKeys.Length,
                    conf.Enabled
                        ? Quartz.Core.MainCore.Tr.Get("KL_HOME_ON", "limiting")
                        : Quartz.Core.MainCore.Tr.Get("KL_HOME_OFF", "off")));
            },
        });
        Context.RegisterImportHandler(new KeyLimiterImport());
        Context.PatchAll(typeof(KeyLimiterModule));
    }
}

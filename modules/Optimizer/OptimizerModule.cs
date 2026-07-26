using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.Optimizer;
public sealed class OptimizerModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(OptimizerModule), $"Quartz.Features.Optimizer.Lang.{lang}.json");
        Optimizer.EnsureConf();
        Optimizer.Initialize();
        Context.AddPage(new NavPage {
            Key = "tweaks.optimizer",
            CategoryKey = "tweaks",
            Order = 20,
            Title = "Optimizer",
            LocaleKey = "SECTION_OPTIMIZER",
            OwnScroll = true,
            Build = PageOptimizer.Create,
        });
        Context.AddHomeCard(new Quartz.UI.Home.HomeCard {
            Key = "optimizer.home",
            Title = "Optimizer",
            LocaleKey = "SECTION_OPTIMIZER",
            Order = 40,
            Build = body => {
                Optimizer.EnsureConf();
                OptimizerSettings conf = Optimizer.Conf;
                if(conf == null) return;
                List<string> on = [];
                if(conf.SmoothGC) on.Add(Quartz.Core.MainCore.Tr.Get("OPT_SMOOTHGC", "Smooth GC"));
                if(conf.LeakGuard) on.Add(Quartz.Core.MainCore.Tr.Get("OPT_LEAKGUARD", "Leak Guard"));
                if(conf.RunInBackground) on.Add(Quartz.Core.MainCore.Tr.Get("OPT_RUNBG", "Run In Background"));
                Quartz.UI.Home.HomeUI.Line(body, on.Count == 0
                    ? Quartz.Core.MainCore.Tr.Get("OPT_HOME_NONE", "No optimizations enabled.")
                    : string.Join(" · ", on));
            },
        });
        Context.PatchAll(typeof(OptimizerModule));
        Context.OnModEnable("Optimizer", Optimizer.Apply);
        Context.OnModDisable("Optimizer", Optimizer.Restore);
    }
    public override void OnTick() => Optimizer.Ticker.Tick();
    public override void OnUnload() {
        Optimizer.Restore();
        Optimizer.Unhook();
    }
}

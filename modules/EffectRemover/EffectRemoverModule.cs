using Quartz.Modules;
using Quartz.UI.Factory.Page;
using Quartz.UI.Nav;
namespace Quartz.Features.EffectRemover;
public sealed class EffectRemoverModule : QuartzModule {
    public override void OnLoad() {
        foreach(string lang in new[] { "en-US", "ko-KR", "zh-CN" })
            Context.RegisterTranslations(typeof(EffectRemoverModule), $"Quartz.Features.EffectRemover.Lang.{lang}.json");
        EffectRemover.EnsureConf();
        Context.AddPage(new NavPage {
            Key = "visuals.effectremover",
            CategoryKey = "visuals",
            Order = 10,
            Title = "Effect Remover",
            LocaleKey = "SECTION_EFFECT_REMOVER",
            Build = PageEffectRemover.Create,
            OwnScroll = true,
        });
        Context.RegisterImportHandler(new EffectRemoverImport());
        Context.PatchAll(typeof(EffectRemoverModule));
        Context.OnModEnable("EffectRemover", EffectRemover.RefreshEditorSaveButtons);
        Context.OnModDisable("EffectRemover", EffectRemover.RestoreEditorSaveButtons);
    }
    public override void OnUnload() => EffectRemover.RestoreEditorSaveButtons();
}

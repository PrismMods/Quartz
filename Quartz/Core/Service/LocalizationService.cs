using Quartz.Async;
using Quartz.Compat;
using Quartz.Compat.Interface;
using Quartz.IO;
using Quartz.Localization;
namespace Quartz.Core.Service;
public sealed class LocalizationService(
    string langPath,
    SettingsFile<CoreSettings> configFile,
    QuartzLogger logger
) : IRuntimeService {
    public Translator Translator { get; } = new();
    public void Initialize() {
        Translator.SetLog(logger.Msg);
        Translator.SetDispatcher(MainThread.Enqueue);
        Translator.Language = configFile.Data.Language;
        _ = LoadThenUpdate();
    }
    // The bundled files load first, so the UI never waits on the network; newer
    // community translations from Quartz-i18n, if any, are folded in afterwards and
    // only then is a second load worth doing. The Translator's dispatcher marshals
    // OnLoadEnd (and so the UI rebuild) back to the main thread — see the r145
    // off-thread launch crash. Failure here is silent by design: the bundled
    // translations stay in place.
    private async Task LoadThenUpdate() {
        try {
            await Translator.Load(langPath);
            if(await LangUpdateService.FetchAsync(langPath) > 0) await Translator.Load(langPath);
        } catch(System.Exception e) {
            logger.Wrn($"[LangUpdate] startup refresh failed: {e.Message}");
        }
    }
    public void Dispose() { }
}

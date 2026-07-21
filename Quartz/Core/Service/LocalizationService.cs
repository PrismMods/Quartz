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

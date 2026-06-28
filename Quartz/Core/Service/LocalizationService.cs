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

        // Route OnLoadEnd / OnLanguageChanged onto the main thread. Load() runs
        // as a background Task whose continuation is not guaranteed to resume on
        // the main thread; its UI-rebuilding subscribers must. MainThread's
        // dispatcher MonoBehaviour is created earlier in runtime startup.
        Translator.SetDispatcher(MainThread.Enqueue);

        Translator.Language = configFile.Data.Language;

        _ = Translator.Load(langPath);
    }

    public void Dispose() { }
}
using Quartz.Core;
namespace Quartz.IO;
public static class FaqFile {
    public static string Error { get; private set; }
    public static List<FaqEntry> Load() {
        Error = null;
        try {
            return FaqDocument.Parse(FaqDocument.Default, MainCore.Conf.Language);
        } catch(Exception e) {
            Error = e.Message;
            MainCore.Log.Err($"[FAQ] couldn't read the built-in questions: {e}");
            return [];
        }
    }
}

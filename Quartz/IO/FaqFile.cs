using Quartz.Core;
using UnityEngine;
namespace Quartz.IO;
public static class FaqFile {
    public static string Error { get; private set; }
    public static string FilePath => MainCore.Paths.FaqPath;
    public static List<FaqEntry> Load() {
        Error = null;
        string path = FilePath;
        try {
            EnsureFile();
            return FaqDocument.Parse(File.ReadAllText(path), MainCore.Conf.Language);
        } catch(Exception e) {
            Error = e.Message;
            MainCore.Log.Err($"[FAQ] couldn't read '{path}': {e}");
            try {
                return FaqDocument.Parse(FaqDocument.Default, MainCore.Conf.Language);
            } catch {
                return [];
            }
        }
    }
    public static void EnsureFile() {
        string path = FilePath;
        if(File.Exists(path)) return;
        AtomicFile.WriteAllText(path, FaqDocument.Default);
        MainCore.Log.Msg($"[FAQ] wrote default '{Path.GetFileName(path)}'");
    }
    public static void OpenFile() {
        try {
            EnsureFile();
            Open(FilePath);
        } catch(Exception e) {
            MainCore.Log.Err($"[FAQ] couldn't open '{FilePath}': {e.Message}");
        }
    }
    public static void OpenFolder() {
        string path = MainCore.Paths.RootPath;
        try {
            Directory.CreateDirectory(path);
            Open(path);
        } catch(Exception e) {
            MainCore.Log.Err($"[FAQ] couldn't open '{path}': {e.Message}");
        }
    }
    private static void Open(string path) => Application.OpenURL("file://" + path.Replace('\\', '/'));
}

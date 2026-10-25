using System.IO;
namespace Quartz.Bootstrap;
// Copies .data-sync/ (Lang) over the data root before the payload loads, then deletes it.
public static class DataSync {
    public const string DirName = ".data-sync";
    public static void Apply(string runtimePath, string dataRoot, Action<string> warn) {
        string source = Path.Combine(runtimePath, DirName);
        if(!Directory.Exists(source)) return;
        bool clean = true;
        foreach(string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) {
            string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destination = Path.Combine(dataRoot, relative);
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? dataRoot);
                File.Copy(file, destination, overwrite: true);
            } catch(Exception e) {
                clean = false;
                warn("could not refresh " + relative + ": " + e.Message);
            }
        }
        if(!clean) return;
        try {
            Directory.Delete(source, true);
        } catch(Exception e) {
            warn("could not remove the staged data files: " + e.Message);
        }
    }
}

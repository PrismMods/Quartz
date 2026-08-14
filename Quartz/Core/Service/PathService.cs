#nullable enable
using Quartz.IO;
namespace Quartz.Core.Service;
public sealed class PathService(string rootPath) {
    public string RootPath { get; } = rootPath;
    public string ConfigPath => Path.Combine(RootPath, "Settings.json");
    public string LangPath => Path.Combine(RootPath, "Lang");
    public string TempPath => Path.Combine(RootPath, "Temp");
    public string ModulePath => Path.Combine(RootPath, "Module");
    public string FontPath => Path.Combine(RootPath, "Fonts");
    public string CustomFontPath => Path.Combine(RootPath, "CustomFonts");
    public string AddonsPath => Path.Combine(RootPath, "Addons");
    public string TufPath => Path.Combine(RootPath, "TUF");
    public string TufLevelsPath => Path.Combine(TufPath, "Levels");
    public string UserResourcePath => Path.Combine(RootPath, "UserResources.json");
    public void Initialize() {
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(LangPath);
        Directory.CreateDirectory(TempPath);
        Directory.CreateDirectory(ModulePath);
        Directory.CreateDirectory(FontPath);
        Directory.CreateDirectory(CustomFontPath);
        Directory.CreateDirectory(AddonsPath);
        Directory.CreateDirectory(TufLevelsPath);
        try {
            // Atomic state lives in root settings, profiles, module metadata,
            // preset/cache folders, and TUF's top level. Skip directories that
            // can contain large user libraries but never receive AtomicFile writes.
            AtomicFile.RecoverTree(
                RootPath, LangPath, TempPath, FontPath, CustomFontPath, AddonsPath, TufLevelsPath
            );
        } catch(Exception e) when(e is IOException or UnauthorizedAccessException) {
            // A damaged recovery artifact must not prevent Quartz from starting;
            // later writes retry recovery for their own destination before saving.
            Diag.Warn(e, "Atomic recovery");
        }
    }
}

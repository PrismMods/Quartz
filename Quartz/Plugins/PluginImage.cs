#nullable enable
using System.Reflection;
using Quartz.Core;
namespace Quartz.Plugins;
public static class PluginImage {
    public static Assembly Load(string path) {
        byte[] image = File.ReadAllBytes(path);
        string pdb = Path.ChangeExtension(path, ".pdb");
        if(File.Exists(pdb)) {
            try {
                return Assembly.Load(image, File.ReadAllBytes(pdb));
            } catch(Exception e) { Diag.Ignore(e); }
        }
        return Assembly.Load(image);
    }
}

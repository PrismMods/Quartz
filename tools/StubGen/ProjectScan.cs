using System.Xml.Linq;

namespace Quartz.StubGen;

/// <summary>
/// Assembly names the build asks for by name. A facade such as UnityEngine.dll holds
/// nothing but type-forwarders, so no type ever resolves into it and the IL-driven scan
/// cannot see it — but the project files still reference it, and a missing stub there
/// surfaces as an unresolved-reference warning rather than a clean compile.
/// </summary>
static class ProjectScan {
    public static IEnumerable<string> ReferencedAssemblies(IEnumerable<string> roots) {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach(string root in roots) {
            if(!Directory.Exists(root)) continue;
            foreach(string file in Files(root)) {
                XDocument document;
                try { document = XDocument.Load(file); } catch { continue; }
                foreach(XElement reference in document.Descendants().Where(e => e.Name.LocalName == "Reference")) {
                    string include = reference.Attribute("Include")?.Value;
                    if(string.IsNullOrEmpty(include)) continue;
                    // Strip a strong-name suffix: `System.IO.Compression, Version=4.2.0.0, ...`
                    names.Add(include.Split(',')[0].Trim());
                }
            }
        }
        return names;
    }

    static IEnumerable<string> Files(string root) {
        foreach(string pattern in new[] { "*.csproj", "*.props", "*.targets", "*.proj" })
            foreach(string file in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)) {
                string[] parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if(parts.Contains("obj") || parts.Contains("bin") || parts.Contains("stubs")) continue;
                yield return file;
            }
    }
}

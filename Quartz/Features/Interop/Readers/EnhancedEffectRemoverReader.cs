using Newtonsoft.Json.Linq;
using Quartz.Interop;
using static Quartz.Features.Interop.ReflectionHelpers;
namespace Quartz.Features.Interop.Readers;
internal static class EnhancedEffectRemoverReader {
    public static int ImportEnhancedEffectRemover(SettingsImportOption option) {
        object settings = GetStaticMember(SettingsImporter.FindType(option, "EnhancedEffectRemover.Settings"), "Instance");
        if(settings != null) {
            int live = ImportRegistry.Deliver(Source(name => GetMemberValue(settings, name)));
            if(live > 0) return live;
        }
        string json = ReadFirstText([Path.Combine(option.Directory ?? "", "Settings.json")]);
        if(string.IsNullOrEmpty(json)) return 0;
        JObject root;
        try {
            root = JObject.Parse(json);
        } catch {
            return 0;
        }
        return ImportRegistry.Deliver(Source(name =>
            root.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out JToken token) ? token : null));
    }
    private static ImportSource Source(Func<string, object> scalar) =>
        new(ImportSourceKind.EnhancedEffectRemover, scalar);
}

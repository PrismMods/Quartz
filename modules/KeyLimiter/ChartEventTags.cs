using ADOFAI;
using Quartz.Core;
namespace Quartz.Features.KeyLimiter;
internal static class ChartEventTags {
    private const string TagProperty = "eventTag";
    private static readonly char[] separators = [' '];
    internal static string[] Split(string raw) =>
        string.IsNullOrEmpty(raw) ? [] : raw.Split(separators, StringSplitOptions.RemoveEmptyEntries);
    internal static IEnumerable<ffxPlusBase> Tagged(ffxPlusBase source, string[] tags) {
        if(source == null || tags == null || tags.Length == 0) yield break;
        List<ffxPlusBase> effects = source.floor == null ? null : source.floor.plusEffects;
        if(effects == null) yield break;
        for(int i = 0; i < effects.Count; i++) {
            ffxPlusBase effect = effects[i];
            if(effect == null || ReferenceEquals(effect, source)) continue;
            if(!Matches(effect, tags)) continue;
            yield return effect;
        }
    }
    private static bool Matches(ffxPlusBase effect, string[] tags) {
        LevelEvent evnt = effect.sourceLevelEvent;
        if(evnt?.info?.propertiesInfo == null) return false;
        if(!evnt.info.propertiesInfo.ContainsKey(TagProperty)) return false;
        string raw;
        try {
            raw = evnt.GetString(TagProperty);
        } catch(Exception e) {
            Diag.Ignore(e);
            return false;
        }
        foreach(string tag in Split(raw)) {
            for(int i = 0; i < tags.Length; i++)
                if(string.Equals(tag, tags[i], StringComparison.Ordinal)) return true;
        }
        return false;
    }
}

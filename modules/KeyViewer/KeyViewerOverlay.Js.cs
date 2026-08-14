using Newtonsoft.Json;

namespace Quartz.Features.KeyViewer;

public static partial class KeyViewerOverlay {
    internal static string JsStatsJson() => JsonConvert.SerializeObject(new {
        kps = pressLog.Count,
        kpsAvg = kpsSamples > 0 ? kpsSum / (float)kpsSamples : 0f,
        kpsMax,
        total = totalCount,
    });

    internal static void ResetJsStats() {
        pressLog.Clear();
        kpsMax = 0;
        kpsSum = 0;
        kpsSamples = 0;
        nextKpsSample = 0f;
    }
}

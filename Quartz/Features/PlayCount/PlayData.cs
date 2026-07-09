using Newtonsoft.Json.Linq;
namespace Quartz.Features.PlayCount;
public sealed class PlayData {
    public float BestProgress;
    public float BestStartProgress;
    public int TotalAttempts;
    public JObject Serialize() => new JObject {
        [nameof(BestProgress)] = BestProgress,
        [nameof(BestStartProgress)] = BestStartProgress,
        [nameof(TotalAttempts)] = TotalAttempts,
    };
    public static PlayData Deserialize(JToken token) {
        PlayData data = new();
        if(token is JObject obj) {
            data.BestProgress = obj.Value<float?>(nameof(BestProgress)) ?? 0f;
            data.BestStartProgress = obj.Value<float?>(nameof(BestStartProgress)) ?? 0f;
            data.TotalAttempts = obj.Value<int?>(nameof(TotalAttempts)) ?? 0;
        }
        return data;
    }
}

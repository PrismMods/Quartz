using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Quartz.UpdateEngine;
// Reflection contract with the frozen bootstrap: one static method taking and returning JSON strings.
public static class EntryPoint {
    public static string Resolve(string requestJson) {
        try {
            JObject root = JObject.Parse(requestJson ?? throw new InvalidOperationException("The update request is empty."));
            UpdateRequest request = new() {
                CurrentVersion = root.Value<string>("CurrentVersion"),
                RuntimeRoot = root.Value<string>("RuntimeRoot"),
                DataRoot = root.Value<string>("DataRoot"),
                FailedVersion = root.Value<string>("FailedVersion"),
            };
            return JsonConvert.SerializeObject(new UpdateManager(request).Resolve());
        } catch(Exception e) {
            return JsonConvert.SerializeObject(new UpdateResult {
                Outcome = UpdateOutcomes.Error,
                Message = e.Message,
            });
        }
    }
}
public static class UpdateOutcomes {
    public const string None = "none";
    public const string Candidate = "candidate";
    public const string Error = "error";
}
public sealed class UpdateRequest {
    public string CurrentVersion;
    public string RuntimeRoot;
    public string DataRoot;
    public string FailedVersion;
}
public sealed class UpdateResult {
    public string Outcome;
    public string Version;
    public string RuntimePath;
    public string Message;
}

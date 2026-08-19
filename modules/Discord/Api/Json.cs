using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz.Core;
namespace Quartz.Features.Discord;
public static class Json {
    public static JToken Parse(string text) {
        using StringReader source = new(text);
        using JsonTextReader reader = new(source) { DateParseHandling = DateParseHandling.None };
        return JToken.ReadFrom(reader);
    }
    public static JToken Prop(JToken token, string key) => token is JObject obj ? obj[key] : null;
    public static string Str(JToken token, string key) {
        JToken value = Prop(token, key);
        return value == null || value.Type != JTokenType.String ? null : value.Value<string>();
    }
    public static string Text(JToken token, string key) {
        string value = Str(token, key);
        return string.IsNullOrEmpty(value) ? null : value;
    }
    public static int? Int(JToken token, string key) {
        JToken value = Prop(token, key);
        return value == null || value.Type != JTokenType.Integer ? null : value.Value<int>();
    }
    public static bool Flag(JToken token, string key) {
        JToken value = Prop(token, key);
        return value != null && value.Type == JTokenType.Boolean && value.Value<bool>();
    }
    public static JArray Arr(JToken token, string key) => Prop(token, key) as JArray;
    public static JObject Obj(JToken token, string key) => Prop(token, key) as JObject;
    public static int Count(JArray array) => array?.Count ?? 0;
    public static ulong Bits(JToken token, string key) {
        JToken value = Prop(token, key);
        if(value == null) return 0UL;
        if(value.Type == JTokenType.String) return ulong.TryParse(value.Value<string>(), out ulong parsed) ? parsed : 0UL;
        if(value.Type != JTokenType.Integer) return 0UL;
        try {
            return value.Value<ulong>();
        } catch(Exception e) {
            Diag.Ignore(e);
            return 0UL;
        }
    }
    public static string Serialize(object payload) => JsonConvert.SerializeObject(payload);
    public static StringContent Body(object payload) =>
        new(Serialize(payload), Encoding.UTF8, "application/json");
}

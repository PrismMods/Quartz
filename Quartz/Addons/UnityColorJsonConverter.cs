using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Quartz.Addons;

/// <summary>
/// Unity's computed Color properties (notably <c>linear</c>) recursively expose
/// another Color to Json.NET. Persist only the four actual color channels.
/// </summary>
internal sealed class UnityColorJsonConverter : JsonConverter {
    public override bool CanConvert(Type objectType) =>
        objectType == typeof(Color) || Nullable.GetUnderlyingType(objectType) == typeof(Color);

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
        Color color = (Color)value;
        writer.WriteStartObject();
        writer.WritePropertyName("r");
        writer.WriteValue(color.r);
        writer.WritePropertyName("g");
        writer.WriteValue(color.g);
        writer.WritePropertyName("b");
        writer.WriteValue(color.b);
        writer.WritePropertyName("a");
        writer.WriteValue(color.a);
        writer.WriteEndObject();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
        if(reader.TokenType == JsonToken.Null) return default(Color);
        JObject value = JObject.Load(reader);
        return new Color(
            Component(value, "r"),
            Component(value, "g"),
            Component(value, "b"),
            Component(value, "a", 1f)
        );
    }

    private static float Component(JObject value, string name, float fallback = 0f) =>
        value.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out JToken token)
        && token.Type != JTokenType.Null
            ? (float)token
            : fallback;
}

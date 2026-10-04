using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

static class UnityColorJsonConverterTests {
    public static void TestColorRoundTrip() {
        JsonSerializerSettings settings = new() {
            Converters = { new Quartz.Addons.UnityColorJsonConverter() },
        };
        Color original = new(0.2f, 0.8f, 1f, 0.5f);

        string json = JsonConvert.SerializeObject(original, settings);
        JObject serialized = JObject.Parse(json);
        Asserts.Assert(serialized.Count == 4, "serialized Color should contain only RGBA channels");
        Asserts.Assert(serialized["r"] != null && serialized["g"] != null
            && serialized["b"] != null && serialized["a"] != null,
            "serialized Color should preserve each channel");

        Color restored = JsonConvert.DeserializeObject<Color>(json, settings);
        Asserts.Assert(restored.r == original.r && restored.g == original.g
            && restored.b == original.b && restored.a == original.a,
            "Color channels should survive a JSON round-trip");

        Color legacy = JsonConvert.DeserializeObject<Color>("{\"R\":0.1,\"G\":0.3,\"B\":0.7}", settings);
        Asserts.Assert(legacy.r == 0.1f && legacy.g == 0.3f
            && legacy.b == 0.7f && legacy.a == 1f,
            "existing channel settings should load case-insensitively with an opaque alpha default");
    }
}

// Minimal test stand-in for Unity's value type. `linear` models the recursively
// exposed computed property that makes Json.NET's default reflection serialization fail.
namespace UnityEngine {
    public struct Color {
        public float r;
        public float g;
        public float b;
        public float a;
        public Color linear => this;

        public Color(float r, float g, float b, float a = 1f) {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }
    }
}

using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.Features.Practice;
public sealed class PracticeBinding {
    public KeyCode Key = KeyCode.None;
    public int Difficulty = 2;
    public int Pitch = 100;
    public bool SetsDifficulty = true;
    public bool SetsSpeed = true;
    public JObject Serialize() => new() {
        [nameof(Key)] = Key.ToString(),
        [nameof(Difficulty)] = Difficulty,
        [nameof(Pitch)] = Pitch,
        [nameof(SetsDifficulty)] = SetsDifficulty,
        [nameof(SetsSpeed)] = SetsSpeed,
    };
    public static PracticeBinding Deserialize(JToken token) {
        PracticeBinding binding = new();
        if(token == null) return binding;
        if(Enum.TryParse(token[nameof(Key)]?.Value<string>() ?? "None", true, out KeyCode key)) binding.Key = key;
        binding.Difficulty = Mathf.Clamp(token[nameof(Difficulty)]?.Value<int>() ?? binding.Difficulty, 0, 2);
        int pitch = token[nameof(Pitch)]?.Value<int>()
            ?? Mathf.RoundToInt((token["Speed"]?.Value<float>() ?? 1f) * 100f);
        binding.Pitch = PracticeSettings.ClampPitch(pitch);
        binding.SetsDifficulty = token[nameof(SetsDifficulty)]?.Value<bool>() ?? binding.SetsDifficulty;
        binding.SetsSpeed = token[nameof(SetsSpeed)]?.Value<bool>() ?? binding.SetsSpeed;
        return binding;
    }
}
public sealed class PracticeSettings : ISettingsFile {
    public const int MinPitch = 1;
    public const int MaxPitch = 1000;
    public bool Enabled = false;
    public bool ShowIndicator = true;
    public bool ShowSpeed = true;
    public bool IndicatorOnlyInGame = true;
    public float FontSize = 26f;
    public float MasterSize = 1f;
    public float OffsetX = 0f;
    public float OffsetY = -110f;
    public float ColorR = 1f, ColorG = 1f, ColorB = 1f, ColorA = 0.85f;
    public readonly List<PracticeBinding> Bindings = [];
    public static int ClampPitch(int value) => Mathf.Clamp(value, MinPitch, MaxPitch);
    public Color GetColor() => IOUtils.Rgba(ColorR, ColorG, ColorB, ColorA);
    public void SetColor(Color c) => IOUtils.SetRgba(c, ref ColorR, ref ColorG, ref ColorB, ref ColorA);
    public JToken Serialize() => new JObject {
        [nameof(Enabled)] = Enabled,
        [nameof(ShowIndicator)] = ShowIndicator,
        [nameof(ShowSpeed)] = ShowSpeed,
        [nameof(IndicatorOnlyInGame)] = IndicatorOnlyInGame,
        [nameof(FontSize)] = FontSize,
        [nameof(MasterSize)] = MasterSize,
        [nameof(OffsetX)] = OffsetX,
        [nameof(OffsetY)] = OffsetY,
        [nameof(ColorR)] = ColorR,
        [nameof(ColorG)] = ColorG,
        [nameof(ColorB)] = ColorB,
        [nameof(ColorA)] = ColorA,
        [nameof(Bindings)] = new JArray(Bindings.Select(b => b.Serialize()).Cast<object>().ToArray()),
    };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        ShowIndicator = IOUtils.Read(token, nameof(ShowIndicator), ShowIndicator);
        ShowSpeed = IOUtils.Read(token, nameof(ShowSpeed), ShowSpeed);
        IndicatorOnlyInGame = IOUtils.Read(token, nameof(IndicatorOnlyInGame), IndicatorOnlyInGame);
        FontSize = IOUtils.Read(token, nameof(FontSize), FontSize);
        MasterSize = IOUtils.Read(token, nameof(MasterSize), MasterSize);
        OffsetX = IOUtils.Read(token, nameof(OffsetX), OffsetX);
        OffsetY = IOUtils.Read(token, nameof(OffsetY), OffsetY);
        IOUtils.ReadRgba(token, "Color", ref ColorR, ref ColorG, ref ColorB, ref ColorA);
        Bindings.Clear();
        if(token[nameof(Bindings)] is not JArray array) return;
        foreach(JToken item in array) {
            if(Bindings.Count >= 16) break;
            Bindings.Add(PracticeBinding.Deserialize(item));
        }
    }
}

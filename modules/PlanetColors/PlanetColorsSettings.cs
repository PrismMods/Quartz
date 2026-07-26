using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.Features.PlanetColors;
public sealed class PlanetColorsSettings : ISettingsFile {
    public const int Slots = 3;
    public bool Enabled = true;
    public bool SeparateTailColor = false;
    public float[] BallR = [1f, 1f, 1f];
    public float[] BallG = [0f, 0f, 0f];
    public float[] BallB = [0f, 0f, 0f];
    public float[] BallOpacity = [0.5f, 0.5f, 0.5f];
    public float[] TailR = [1f, 1f, 1f];
    public float[] TailG = [0f, 0f, 0f];
    public float[] TailB = [0f, 0f, 0f];
    public float[] TailOpacity = [0f, 0f, 0f];
    public bool EnableRingRecolor = false;
    public float RingR = 1f, RingG = 1f, RingB = 1f, RingA = 1f;
    public bool SeparateRingColor = false;
    public float[] SlotRingR = [1f, 1f, 1f];
    public float[] SlotRingG = [1f, 1f, 1f];
    public float[] SlotRingB = [1f, 1f, 1f];
    public float[] SlotRingA = [1f, 1f, 1f];
    public bool EnableOverlay = false;
    public string[] OverlayPath = ["", "", ""];
    public float[] OverlayScale = [0.05f, 0.05f, 0.05f];
    public const float MinOverlayScale = 0.001f;
    public const float MaxOverlayScale = 5f;
    public Color GetRingColor() => new(
        Mathf.Clamp01(RingR), Mathf.Clamp01(RingG), Mathf.Clamp01(RingB), Mathf.Clamp01(RingA)
    );
    public Color GetRingColor(int slot) {
        if(!SeparateRingColor) return GetRingColor();
        slot = Mathf.Clamp(slot, 0, Slots - 1);
        return new Color(
            Mathf.Clamp01(SlotRingR[slot]),
            Mathf.Clamp01(SlotRingG[slot]),
            Mathf.Clamp01(SlotRingB[slot]),
            Mathf.Clamp01(SlotRingA[slot])
        );
    }
    public void SetRingRgb(Color c) {
        RingR = Mathf.Clamp01(c.r);
        RingG = Mathf.Clamp01(c.g);
        RingB = Mathf.Clamp01(c.b);
    }
    public void SetSlotRingRgb(int slot, Color c) {
        slot = Mathf.Clamp(slot, 0, Slots - 1);
        SlotRingR[slot] = Mathf.Clamp01(c.r);
        SlotRingG[slot] = Mathf.Clamp01(c.g);
        SlotRingB[slot] = Mathf.Clamp01(c.b);
    }
    public string GetOverlayPath(int slot) {
        slot = Mathf.Clamp(slot, 0, Slots - 1);
        return OverlayPath[slot] ?? "";
    }
    public void SetOverlayPath(int slot, string path) {
        slot = Mathf.Clamp(slot, 0, Slots - 1);
        OverlayPath[slot] = path ?? "";
    }
    public float GetOverlayScale(int slot) {
        slot = Mathf.Clamp(slot, 0, Slots - 1);
        return Mathf.Clamp(OverlayScale[slot], MinOverlayScale, MaxOverlayScale);
    }
    public Color GetBallColor(int slot) {
        slot = Mathf.Clamp(slot, 0, Slots - 1);
        return new Color(
            Mathf.Clamp01(BallR[slot]),
            Mathf.Clamp01(BallG[slot]),
            Mathf.Clamp01(BallB[slot]),
            Mathf.Clamp01(BallOpacity[slot])
        );
    }
    public void SetBallRgb(int slot, Color c) {
        slot = Mathf.Clamp(slot, 0, Slots - 1);
        BallR[slot] = Mathf.Clamp01(c.r);
        BallG[slot] = Mathf.Clamp01(c.g);
        BallB[slot] = Mathf.Clamp01(c.b);
    }
    public Color GetTailColor(int slot) {
        slot = Mathf.Clamp(slot, 0, Slots - 1);
        return SeparateTailColor
            ? new Color(
                Mathf.Clamp01(TailR[slot]),
                Mathf.Clamp01(TailG[slot]),
                Mathf.Clamp01(TailB[slot]),
                Mathf.Clamp01(TailOpacity[slot])
            )
            : new Color(
                Mathf.Clamp01(BallR[slot]),
                Mathf.Clamp01(BallG[slot]),
                Mathf.Clamp01(BallB[slot]),
                Mathf.Clamp01(TailOpacity[slot])
            );
    }
    public void SetTailRgb(int slot, Color c) {
        slot = Mathf.Clamp(slot, 0, Slots - 1);
        TailR[slot] = Mathf.Clamp01(c.r);
        TailG[slot] = Mathf.Clamp01(c.g);
        TailB[slot] = Mathf.Clamp01(c.b);
    }
    public JToken Serialize() => new JObject {
            [nameof(Enabled)] = Enabled,
            [nameof(SeparateTailColor)] = SeparateTailColor,
            [nameof(BallR)] = new JArray(BallR),
            [nameof(BallG)] = new JArray(BallG),
            [nameof(BallB)] = new JArray(BallB),
            [nameof(BallOpacity)] = new JArray(BallOpacity),
            [nameof(TailR)] = new JArray(TailR),
            [nameof(TailG)] = new JArray(TailG),
            [nameof(TailB)] = new JArray(TailB),
            [nameof(TailOpacity)] = new JArray(TailOpacity),
            [nameof(EnableRingRecolor)] = EnableRingRecolor,
            [nameof(RingR)] = RingR,
            [nameof(RingG)] = RingG,
            [nameof(RingB)] = RingB,
            [nameof(RingA)] = RingA,
            [nameof(SeparateRingColor)] = SeparateRingColor,
            [nameof(SlotRingR)] = new JArray(SlotRingR),
            [nameof(SlotRingG)] = new JArray(SlotRingG),
            [nameof(SlotRingB)] = new JArray(SlotRingB),
            [nameof(SlotRingA)] = new JArray(SlotRingA),
            [nameof(EnableOverlay)] = EnableOverlay,
            [nameof(OverlayPath)] = new JArray(OverlayPath),
            [nameof(OverlayScale)] = new JArray(OverlayScale),
    };
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        SeparateTailColor = IOUtils.Read(token, nameof(SeparateTailColor), SeparateTailColor);
        ReadFloats(token, nameof(BallR), BallR);
        ReadFloats(token, nameof(BallG), BallG);
        ReadFloats(token, nameof(BallB), BallB);
        ReadFloats(token, nameof(BallOpacity), BallOpacity);
        ReadFloats(token, nameof(TailR), TailR);
        ReadFloats(token, nameof(TailG), TailG);
        ReadFloats(token, nameof(TailB), TailB);
        ReadFloats(token, nameof(TailOpacity), TailOpacity);
        EnableRingRecolor = IOUtils.Read(token, nameof(EnableRingRecolor), EnableRingRecolor);
        RingR = Mathf.Clamp01(IOUtils.Read(token, nameof(RingR), RingR));
        RingG = Mathf.Clamp01(IOUtils.Read(token, nameof(RingG), RingG));
        RingB = Mathf.Clamp01(IOUtils.Read(token, nameof(RingB), RingB));
        RingA = Mathf.Clamp01(IOUtils.Read(token, nameof(RingA), RingA));
        SeparateRingColor = IOUtils.Read(token, nameof(SeparateRingColor), SeparateRingColor);
        FillSlots(SlotRingR, RingR);
        FillSlots(SlotRingG, RingG);
        FillSlots(SlotRingB, RingB);
        FillSlots(SlotRingA, RingA);
        ReadFloats(token, nameof(SlotRingR), SlotRingR);
        ReadFloats(token, nameof(SlotRingG), SlotRingG);
        ReadFloats(token, nameof(SlotRingB), SlotRingB);
        ReadFloats(token, nameof(SlotRingA), SlotRingA);
        EnableOverlay = IOUtils.Read(token, nameof(EnableOverlay), EnableOverlay);
        ReadStrings(token, nameof(OverlayPath), OverlayPath);
        ReadFloats(token, nameof(OverlayScale), OverlayScale, MinOverlayScale, MaxOverlayScale);
    }
    private static void FillSlots(float[] target, float value) {
        for(int i = 0; i < target.Length; i++) target[i] = value;
    }
    private static void ReadFloats(JToken token, string name, float[] target)
        => ReadFloats(token, name, target, 0f, 1f);
    private static void ReadFloats(JToken token, string name, float[] target, float min, float max) {
        if(token?[name] is not JArray arr) return;
        for(int i = 0; i < target.Length && i < arr.Count; i++) {
            try { target[i] = Mathf.Clamp((float)arr[i], min, max); } catch { }
        }
    }
    private static void ReadStrings(JToken token, string name, string[] target) {
        if(token?[name] is not JArray arr) return;
        for(int i = 0; i < target.Length && i < arr.Count; i++) {
            try { target[i] = arr[i]?.Type == JTokenType.Null ? "" : (string)arr[i] ?? ""; } catch { }
        }
    }
}

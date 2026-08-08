using Quartz.Core;
using Quartz.Utility;
using UnityEngine;
namespace Quartz.Features.KeyLimiter;
public sealed class ChartKeyLimiterState {
    public static readonly ChartKeyLimiterState Instance = new();
    private const int MaxPlayers = 4;
    private readonly HashSet<KeyCode>[] usedKeys = new HashSet<KeyCode>[MaxPlayers];
    private bool active;
    private int limit = int.MaxValue;
    private KeyExceedMethod exceedMethod = KeyExceedMethod.Ignore;
    private string[] targetTags = [];
    private RunEventBehaviour runBehaviour = RunEventBehaviour.Once;
    private bool ranOnce;
    private FfxKeyLimiter source;
    public string Message { get; private set; } = "";
    public bool Active => active;
    private ChartKeyLimiterState() {
        for(int i = 0; i < usedKeys.Length; i++) usedKeys[i] = [];
    }
    public void Clear() => Set(null, false, int.MaxValue, KeyExceedMethod.Ignore, [], RunEventBehaviour.Once, "");
    public void Set(
        FfxKeyLimiter effect,
        bool enabled,
        int keyLimit,
        KeyExceedMethod method,
        string[] tags,
        RunEventBehaviour behaviour,
        string message
    ) {
        source = effect;
        active = enabled;
        limit = keyLimit < 0 ? 0 : keyLimit;
        exceedMethod = method;
        targetTags = tags ?? [];
        runBehaviour = behaviour;
        ranOnce = false;
        Message = message ?? "";
        for(int i = 0; i < usedKeys.Length; i++) usedKeys[i].Clear();
    }
    public bool IsKeyValid(int playerId, KeyCode key) {
        if(!active) return true;
        if(key == KeyCode.None) return true;
        if(playerId < 0 || playerId >= usedKeys.Length) return true;
        KeyCode normalized = KeyCodes.Normalize(key);
        HashSet<KeyCode> used = usedKeys[playerId];
        if(used.Contains(normalized)) return true;
        if(used.Count >= limit) return false;
        used.Add(normalized);
        return true;
    }
    public bool OnInvalidPress(ref HitMargin margin) {
        if(!active) return false;
        if(exceedMethod is KeyExceedMethod.RunEvent
            or KeyExceedMethod.IgnoreAndRunEvent
            or KeyExceedMethod.KillAndRunEvent) RunTaggedEvents();
        if(exceedMethod != KeyExceedMethod.RunEvent) margin = HitMargin.OverPress;
        return exceedMethod is KeyExceedMethod.Kill or KeyExceedMethod.KillAndRunEvent;
    }
    private void RunTaggedEvents() {
        if(source == null || targetTags.Length == 0) return;
        if(ranOnce && runBehaviour == RunEventBehaviour.Once) return;
        try {
            foreach(ffxPlusBase effect in ChartEventTags.Tagged(source, targetTags)) {
                effect.StartEffectWithOffset(null);
                ranOnce = true;
            }
        } catch(Exception e) {
            Diag.Warn(e, "running the events tagged by a chart key limiter");
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace Quartz.Features.KeyViewer;

internal sealed class RainManager : MonoBehaviour {
    private RainGraphic graphic;
    private readonly List<RawRain>[] groups = [new(64), new(64), new(64)];
    private readonly Queue<RawRain> pending = new(64);
    private readonly Stack<RawRain> pool = new(64);
    private const int PoolCap = 256;
    public RawRain Rent() {
        if(pool.Count == 0) return new RawRain();
        RawRain raw = pool.Pop();
        raw.Reset();
        return raw;
    }
    public void SetLayer(RectTransform value) {
        pending.Clear();
        for(int i = 0; i < groups.Length; i++) groups[i].Clear();
        if(graphic != null) {
            Destroy(graphic.gameObject);
            graphic = null;
        }
        if(value == null) return;
        GameObject obj = new("RainDrops");
        obj.transform.SetParent(value, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        graphic = obj.AddComponent<RainGraphic>();
        graphic.raycastTarget = false;
        graphic.color = Color.white;
        graphic.SetSource(groups);
        enabled = true;
    }
    public void Enqueue(RawRain raw) {
        if(raw == null) return;
        pending.Enqueue(raw);
        if(!enabled) enabled = true;
    }
    private bool IsIdle() {
        if(pending.Count > 0) return false;
        for(int i = 0; i < groups.Length; i++)
            if(groups[i].Count > 0) return false;
        return true;
    }
    public void Clear() {
        pending.Clear();
        for(int i = 0; i < groups.Length; i++) groups[i].Clear();
        if(graphic != null) graphic.SetVerticesDirty();
    }
    private void LateUpdate() {
        if(graphic == null) {
            pending.Clear();
            return;
        }
        if(pending.Count == 0) return;
        while(pending.Count > 0) {
            RawRain raw = pending.Dequeue();
            List<RawRain> group = groups[Mathf.Clamp(raw.Group, 1, 3) - 1];
            int count = group.Count;
            if(count == 0 || group[count - 1].Order <= raw.Order) {
                group.Add(raw);
                continue;
            }
            int at = count;
            for(int i = 0; i < count; i++) {
                if(group[i].Order > raw.Order) {
                    at = i;
                    break;
                }
            }
            group.Insert(at, raw);
        }
        graphic.SetFrame(KvClock.Now);
    }
    private void Update() {
        if(graphic == null) {
            pending.Clear();
            return;
        }
        bool dirty = false;
        float now = KvClock.Now;
        for(int g = 0; g < groups.Length; g++) {
            List<RawRain> active = groups[g];
            int write = 0;
            for(int read = 0; read < active.Count; read++) {
                RawRain raw = active[read];
                float trail = raw.EndTime < 0f ? 0f : Mathf.Max(0f, (now - raw.EndTime) * raw.Speed);
                if(trail <= raw.TrackHeight + 8f) {
                    if(write != read) active[write] = raw;
                    write++;
                    if(raw.EndTime >= 0f || (now - raw.StartTime) * raw.Speed < raw.TrackHeight) dirty = true;
                    continue;
                }
                dirty = true;
                if(pool.Count < PoolCap) pool.Push(raw);
            }
            if(write < active.Count) active.RemoveRange(write, active.Count - write);
        }
        if(dirty && pending.Count == 0) graphic.SetFrame(now);
        if(IsIdle()) enabled = false;
    }
}

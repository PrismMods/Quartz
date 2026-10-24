using System.Diagnostics;
using System.Threading;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.KeyViewer;
internal static class KvClock {
    private static readonly long Origin = Stopwatch.GetTimestamp();
    private static readonly double TicksToSeconds = 1.0 / Stopwatch.Frequency;
    private static Func<double> external;
    private static double externalBase;
    private static double nowAtSwitch;
    private static double wallOffset;
    private static double Wall => (Stopwatch.GetTimestamp() - Origin) * TicksToSeconds;
    public static float Now {
        get {
            Func<double> ext = external;
            if(ext == null) return (float)(Wall + wallOffset);
            try {
                return (float)(nowAtSwitch + ext() - externalBase);
            } catch(Exception e) {
                Diag.Ignore(e);
                return (float)(Wall + wallOffset);
            }
        }
    }
    // An external driver (offline render capture) swaps the wall clock for its own virtual clock so
    // rain lengths and animation speeds follow the capture timeline, not real time. Both switches
    // rebase so Now never jumps backwards for timestamps already stored in boxes and rains.
    internal static void SetExternal(Func<double> clock) {
        if(clock == null) {
            ClearExternal();
            return;
        }
        double baseValue;
        try {
            baseValue = clock();
        } catch(Exception e) {
            Diag.Ignore(e);
            return;
        }
        ClearExternal();
        nowAtSwitch = Wall + wallOffset;
        externalBase = baseValue;
        external = clock;
    }
    internal static void ClearExternal() {
        Func<double> ext = external;
        if(ext == null) return;
        double current;
        try {
            current = nowAtSwitch + ext() - externalBase;
        } catch(Exception e) {
            Diag.Ignore(e);
            current = Wall + wallOffset;
        }
        external = null;
        wallOffset = current - Wall;
    }
}
public static class KvInputQueue {
    public struct Ev {
        public int Seq;
        public int Key;
        public bool Down;
        public float Time;
    }
    private const int Capacity = 2048;
    private const int Mask = Capacity - 1;
    private static readonly Ev[] Ring = new Ev[Capacity];
    private static int writeIndex;
    private static int readIndex;
    private static int readShared;
    private static int overflowed;
    private static volatile bool focused = true;
    private static volatile bool wanted;
    private static volatile bool hookActive;
    private static volatile bool externalDriver;
    private static float nextHookCheck;
    public static bool HookActive => externalDriver || hookActive;
    public static void SetWanted(bool value) => wanted = value;
    public static void SetFocused(bool value) => focused = value;
    public static void Push(KeyCode key, bool down) {
        // Real hook events are ignored while an external driver replays a run; the machine's
        // keyboard is not part of the capture.
        if(externalDriver) return;
        if(!wanted || key == KeyCode.None) return;
        if(down && !focused) return;
        Write(key, down);
    }
    // Driver path: no wanted/focus gating — the driver decides what the viewer shows, and a
    // background render has no window focus to speak of.
    internal static void PushExternal(KeyCode key, bool down) {
        if(!externalDriver || key == KeyCode.None) return;
        Write(key, down);
    }
    internal static void SetExternalDriver(bool value) {
        if(externalDriver == value) return;
        externalDriver = value;
        Discard();
        if(!value) {
            hookActive = false;
            nextHookCheck = 0f;
        }
    }
    private static void Write(KeyCode key, bool down) {
        int w = writeIndex;
        if(w - Volatile.Read(ref readShared) >= Capacity) {
            Volatile.Write(ref overflowed, 1);
            return;
        }
        int slot = w & Mask;
        Ring[slot].Key = (int)key;
        Ring[slot].Down = down;
        Ring[slot].Time = KvClock.Now;
        Volatile.Write(ref Ring[slot].Seq, w + 1);
        writeIndex = w + 1;
    }
    public static void Drain(List<Ev> into) {
        int r = readIndex;
        while(true) {
            int slot = r & Mask;
            if(Volatile.Read(ref Ring[slot].Seq) != r + 1) break;
            into.Add(Ring[slot]);
            r++;
        }
        Commit(r);
    }
    public static void Discard() {
        int r = readIndex;
        while(Volatile.Read(ref Ring[r & Mask].Seq) == r + 1) r++;
        Commit(r);
        Volatile.Write(ref overflowed, 0);
    }
    private static void Commit(int r) {
        if(r == readIndex) return;
        readIndex = r;
        Volatile.Write(ref readShared, r);
    }
    public static bool TakeOverflow() => Interlocked.Exchange(ref overflowed, 0) != 0;
    public static void RecoverFromGap() {
        int highest = readIndex;
        for(int i = 0; i < Capacity; i++) {
            int seq = Volatile.Read(ref Ring[i].Seq);
            if(seq > highest) highest = seq;
        }
        Commit(highest);
    }
    public static void Pump(float now, bool enabled) => EnsureHook(now, enabled);
    private static void EnsureHook(float now, bool enabled) {
        if(externalDriver) return;
        if(!enabled) {
            hookActive = false;
            return;
        }
        if(now < nextHookCheck) return;
        nextHookCheck = now + HookCheckIntervalSeconds;
        try {
            hookActive = AsyncInputManager.isActive;
        } catch(Exception e) {
            Diag.Ignore(e);
            hookActive = false;
        }
    }
    public static void Shutdown() {
        wanted = false;
        hookActive = false;
        Discard();
        nextHookCheck = 0f;
    }
    private const float HookCheckIntervalSeconds = 0.5f;
    public static void Reset() {
        Discard();
        nextHookCheck = 0f;
    }
}

using System;
using System.Reflection;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Countdown;
internal static class CountdownAsyncClock {
    private const BindingFlags Statics =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
    private static bool resolved;
    private static FieldInfo prevFrameTick;
    private static FieldInfo currFrameTick;
    private static FieldInfo previousFrameTime;
    private static FieldInfo offsetTick;
    private static FieldInfo offsetTickUpdated;
    private static bool warned;
    internal static bool Available {
        get {
            Resolve();
            if(!Resolved && !warned) {
                warned = true;
                CountdownWorld.Log(
                    "async input clock fields are missing on this game build; skipping the post-freeze rebase");
            }
            return Resolved;
        }
    }
    private static bool Resolved {
        get {
            return prevFrameTick != null
                && currFrameTick != null
                && previousFrameTime != null
                && offsetTick != null
                && offsetTickUpdated != null;
        }
    }
    internal static ulong OffsetTick {
        get {
            Resolve();
            return Read(offsetTick) is ulong value ? value : 0UL;
        }
    }
    internal static void Rebase(ulong nowTick, ulong newOffsetTick) {
        if(!Available) return;
        Write(prevFrameTick, nowTick);
        Write(currFrameTick, nowTick);
        Write(previousFrameTime, Time.unscaledTimeAsDouble);
        Write(offsetTick, newOffsetTick);
        Write(offsetTickUpdated, true);
    }
    private static void Resolve() {
        if(resolved) return;
        resolved = true;
        try {
            Type type = typeof(AsyncInputManager);
            prevFrameTick = type.GetField("prevFrameTick", Statics);
            currFrameTick = type.GetField("currFrameTick", Statics);
            previousFrameTime = type.GetField("previousFrameTime", Statics);
            offsetTick = type.GetField("offsetTick", Statics);
            offsetTickUpdated = type.GetField("offsetTickUpdated", Statics);
        } catch(Exception e) {
            Diag.Warn(e, "Countdown/AsyncClock");
        }
    }
    private static object Read(FieldInfo field) {
        try {
            return field?.GetValue(null);
        } catch(Exception e) {
            Diag.Ignore(e);
            return null;
        }
    }
    private static void Write(FieldInfo field, object value) {
        try {
            field?.SetValue(null, value);
        } catch(Exception e) {
            Diag.Ignore(e);
        }
    }
}

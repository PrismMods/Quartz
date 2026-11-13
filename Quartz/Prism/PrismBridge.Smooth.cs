using System.Runtime.CompilerServices;
using Quartz.Core;
using Quartz.UI;
using Quartz.UI.FacetUi;
namespace Quartz.Prism;
internal static partial class PrismBridge {
    /// <summary>
    /// PrismLib's Smooth scrolling setting (scroll.smooth), shared by every Prism mod: every Facet window and Quartz's classic
    /// menu glide to where the wheel sends them when it is on. Read through Config by PrismRules.SmoothOn, never through
    /// Prism.SmoothScroll, which a published PrismLib lacks (the key const compiles to its literal). Read whenever a PrismLib
    /// is loaded, even one Quartz could not wire to (Register threw, or another mod's copy is below Quartz's floor): the
    /// player's value still decides. On only without PrismLib, or once a read failed.
    /// </summary>
    internal static bool SmoothScroll {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get {
#if !IL2CPP && !QUARTZ_KEYVIEWER
            if((loaded || ForeignLib) && !smoothGone) {
                try { return PrismRules.SmoothOn(SmoothRaw()); } catch(Exception e) { SmoothOff(e); }
            }
#endif
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        set {
#if !IL2CPP && !QUARTZ_KEYVIEWER
            if(!Available || smoothGone) return;
            try { SmoothSet(value); } catch(Exception e) { SmoothOff(e); }
#endif
        }
    }
    // Per frame, in every flavour and with or without PrismLib.UI: Facet's flag follows the setting (the other Prism mods
    // copy the same value), and a change from anywhere marks the classic Settings row stale, rebuilt on its next open. After a
    // failed read the "on" fallback is written too: a read fails only on a PrismLib older than Config, where every mod's read
    // fails alike, so no other mod writes the flag.
    private static int smoothSeen = -1;
    private static void TickSmooth() {
        bool on = SmoothScroll;
        FacetBridge.SetSmoothScroll(on);
        int now = on ? 1 : 0;
        if(smoothSeen >= 0 && smoothSeen != now) UICore.ClassicStale = true;
        smoothSeen = now;
    }
#if !IL2CPP && !QUARTZ_KEYVIEWER
    private static bool smoothGone;
    private static void SmoothOff(Exception e) {
        smoothGone = true;
        MainCore.Log.Wrn($"PrismLib smooth scrolling unavailable: {e.Message}");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string SmoothRaw() => PrismLib.Config.Get(PrismLib.Prism.SmoothKey);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SmoothSet(bool on) => PrismLib.Config.SetBool(PrismLib.Prism.SmoothKey, on);
#endif
#if DEBUG
    /// <summary>
    /// Self-tests: PrismLib's settings file as it is now. The action writes those bytes back (or deletes a file that was not
    /// there) and drops Config's cache, so a missing scroll.smooth stays missing. A no-op without PrismLib.
    /// </summary>
    internal static Action KeepPrismConfig() {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(Available) {
            string path = ConfigPath();
            byte[] bytes = path != null && File.Exists(path) ? File.ReadAllBytes(path) : null;
            return () => {
                if(path == null) return;
                if(bytes != null) File.WriteAllBytes(path, bytes);
                else if(File.Exists(path)) File.Delete(path);
                DropConfigCache();
            };
        }
#endif
        return () => { };
    }
    /// <summary>
    /// Self-tests: scroll.smooth as stored (null when missing or without PrismLib), and a raw write of it; null drops it from
    /// Config's cache only, so it reads missing until KeepPrismConfig's action puts the file back.
    /// </summary>
    internal static string SmoothStored {
        get {
#if !IL2CPP && !QUARTZ_KEYVIEWER
            if(Available) return SmoothRaw();
#endif
            return null;
        }
    }
    internal static void StoreSmooth(string value) {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(Available) StoreSmoothImpl(value);
#endif
    }
    /// <summary>Self-tests: Prism · Settings' own Smooth scrolling row, written through its Set as that window does.</summary>
    internal static bool SetSmoothEntry(bool on) {
#if !IL2CPP && !QUARTZ_KEYVIEWER
        if(Available) return SetSmoothEntryImpl(on);
#endif
        return false;
    }
#endif
#if DEBUG && !IL2CPP && !QUARTZ_KEYVIEWER
    /// <summary>
    /// Self-tests: Quartz's link to PrismLib down until the action runs, with PrismLib loaded by Quartz (Register threw), by
    /// another mod only (a copy below Quartz's floor), or not at all; readFailed also marks the setting unreadable, as a
    /// failed read does. The action puts the real state back.
    /// </summary>
    internal static Action LinkDown(bool loadedLib, bool foreignLib, bool readFailed = false) {
        bool available = Available, wasLoaded = loaded, foreign = ForeignLib, gone = smoothGone;
        Available = false;
        loaded = loadedLib;
        ForeignLib = foreignLib;
        smoothGone = readFailed;
        return () => {
            Available = available;
            loaded = wasLoaded;
            ForeignLib = foreign;
            smoothGone = gone;
        };
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool SetSmoothEntryImpl(bool on) {
        foreach(var kv in PrismLib.Settings.Of("PrismLib")) {
            if(kv.Value.Id != PrismLib.Prism.SmoothKey) continue;
            kv.Value.Set(on);
            return true;
        }
        return false;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void StoreSmoothImpl(string value) {
        if(value != null) PrismLib.Config.Set(PrismLib.Prism.SmoothKey, value);
        else if(PrismLib.Config.Has(PrismLib.Prism.SmoothKey)) ((Dictionary<string, string>)ConfigCache().GetValue(null)).Remove(PrismLib.Prism.SmoothKey);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ConfigPath() => PrismLib.Config.FilePath;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DropConfigCache() => ConfigCache().SetValue(null, null);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static System.Reflection.FieldInfo ConfigCache() =>
        typeof(PrismLib.Config).GetField("_values", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
#endif
}

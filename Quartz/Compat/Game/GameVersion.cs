using System;
using System.Reflection;
using Quartz.Core;
namespace Quartz.Compat.Game;
public static class GameVersion {
    public const int LastLegacyRelease = 136;
    private static bool resolved;
    private static int release;
    public static int Release {
        get {
            if(!resolved) Resolve();
            return release;
        }
    }
    public static bool IsLegacy => Release != 0 && Release <= LastLegacyRelease;
    public static string DisplayRelease => Release == 0 ? "r?" : "r" + Release;
    private static void Resolve() {
        resolved = true;
        try {
            Assembly game = typeof(ADOBase).Assembly;
            foreach(string holder in new[] { "Releases", "GCNS" }) {
                FieldInfo f = game.GetType(holder)?.GetField(
                    "releaseNumber", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if(f == null) continue;
                object raw = f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null);
                if(raw is not int i) continue;
                release = i;
                return;
            }
        } catch(Exception e) {
            Diag.Ignore(e);
            release = 0;
        }
    }
}

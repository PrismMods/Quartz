using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Quartz.Core;
using Quartz.IO;
using UnityEngine;
namespace Quartz.Update;
public enum RestartMethod {
    Unavailable,
    Steam,
    Executable,
}
public static class GameRestarter {
    private static bool resolved;
    private static RestartMethod method;
    private static string steamUrl;
    private static string launchPath;
    private static bool launchIsMacBundle;
    public static RestartMethod Method {
        get {
            Resolve();
            return method;
        }
    }
    public static bool Available => Method != RestartMethod.Unavailable;
    public static bool Restart() {
        Resolve();
        if(method == RestartMethod.Unavailable) return false;
        try {
            SettingsRegistry.SaveAll();
        } catch(Exception e) {
            Diag.Warn(e, "Update");
        }
        bool spawned;
        try {
            spawned = Spawn();
        } catch(Exception e) {
            MainCore.Log.Wrn("[Update] restart spawn failed: " + e.Message);
            return false;
        }
        if(!spawned) return false;
        MainCore.Log.Msg($"[Update] restarting via {method}");
        try {
            Application.Quit();
        } catch(Exception e) {
            MainCore.Log.Wrn("[Update] Application.Quit failed: " + e.Message);
            return false;
        }
        return true;
    }
    private static void Resolve() {
        if(resolved) return;
        resolved = true;
        try {
            ResolveCore();
        } catch(Exception e) {
            Diag.Warn(e, "Update");
            method = RestartMethod.Unavailable;
        }
    }
    private static void ResolveCore() {
        uint appId = SteamAppId();
        if(appId != 0) {
            steamUrl = "steam://rungameid/" + appId.ToString(CultureInfo.InvariantCulture);
            method = RestartMethod.Steam;
            return;
        }
        launchPath = ExecutablePath();
        if(string.IsNullOrEmpty(launchPath)) {
            method = RestartMethod.Unavailable;
            return;
        }
        launchIsMacBundle = IsMac
            && launchPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase);
        method = RestartMethod.Executable;
    }
    private static bool IsWindows =>
        Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor;
    private static bool IsMac =>
        Application.platform is RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor;
    private static uint SteamAppId() {
        uint id = EnvAppId("SteamAppId");
        if(id == 0) id = EnvAppId("SteamGameId");
        if(id == 0) id = SteamworksAppId();
        return id;
    }
    private static uint EnvAppId(string variable) {
        try {
            string raw = Environment.GetEnvironmentVariable(variable);
            if(string.IsNullOrWhiteSpace(raw)) return 0;
            return ulong.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ulong value)
                && value is > 0 and <= uint.MaxValue
                ? (uint)value
                : 0;
        } catch(Exception e) {
            Diag.Ignore(e);
            return 0;
        }
    }
    private static uint SteamworksAppId() {
        try {
            Type manager = AccessTools.TypeByName("SteamManager");
            PropertyInfo initialized = manager == null ? null : AccessTools.Property(manager, "Initialized");
            if(initialized?.GetValue(null) is not true) return 0;
            Type utils = AccessTools.TypeByName("Steamworks.SteamUtils");
            MethodInfo getAppId = utils == null ? null : AccessTools.Method(utils, "GetAppID");
            object appId = getAppId?.Invoke(null, null);
            if(appId == null) return 0;
            return AccessTools.Field(appId.GetType(), "m_AppId")?.GetValue(appId) is uint value ? value : 0;
        } catch(Exception e) {
            Diag.Ignore(e);
            return 0;
        }
    }
    private static string MainModulePath() => Process.GetCurrentProcess().MainModule?.FileName;
    private static string ExecutablePath() {
        string binary = null;
        try {
            binary = MainModulePath();
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        if(IsMac) {
            string bundle = MacAppBundle(binary) ?? MacAppBundle(Application.dataPath);
            if(!string.IsNullOrEmpty(bundle)) return bundle;
        }
        return !string.IsNullOrEmpty(binary) && File.Exists(binary) ? binary : FromDataPath();
    }
    private static string MacAppBundle(string path) {
        if(string.IsNullOrEmpty(path)) return null;
        try {
            for(DirectoryInfo dir = Directory.Exists(path) ? new(path) : new FileInfo(path).Directory;
                dir != null;
                dir = dir.Parent) {
                if(dir.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) return dir.FullName;
            }
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        return null;
    }
    private static string FromDataPath() {
        try {
            string data = Application.dataPath;
            if(string.IsNullOrEmpty(data)) return null;
            DirectoryInfo dataDir = new(data);
            string root = dataDir.Parent?.FullName;
            if(root == null || !dataDir.Name.EndsWith("_Data", StringComparison.Ordinal)) return null;
            string name = dataDir.Name.Substring(0, dataDir.Name.Length - "_Data".Length);
            string[] candidates = IsWindows ? [name + ".exe"] : [name, name + ".x86_64"];
            foreach(string candidate in candidates) {
                string full = Path.Combine(root, candidate);
                if(File.Exists(full)) return full;
            }
        } catch(Exception e) {
            Diag.Ignore(e);
        }
        return null;
    }
    private static bool Spawn() {
        try {
            string dir = Path.Combine(MainCore.Paths.TempPath, "Restart");
            Directory.CreateDirectory(dir);
            string script = Path.Combine(dir, IsWindows ? "restart.cmd" : "restart.sh");
            File.WriteAllText(script, IsWindows ? WindowsScript() : UnixScript());
            ProcessStartInfo info = new() {
                FileName = IsWindows ? "cmd.exe" : "/bin/sh",
                Arguments = (IsWindows ? "/c " : "") + "\"" + script + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = dir,
            };
            if(IsWindows) {
                info.EnvironmentVariables["QUARTZ_RESTART_TARGET"] =
                    method == RestartMethod.Steam ? steamUrl : launchPath;
                info.EnvironmentVariables["QUARTZ_RESTART_CWD"] = WorkingDir() ?? "";
                info.EnvironmentVariables["QUARTZ_RESTART_ARGS"] = method == RestartMethod.Executable
                    ? string.Join(" ", ForwardedArgs().Select(BatchQuote))
                    : "";
            }
            using Process spawned = Process.Start(info);
            return spawned != null;
        } catch(Exception e) {
            MainCore.Log.Wrn("[Update] restart spawn failed: " + e.Message);
            return false;
        }
    }
    private static int CurrentPid() {
        try {
            return Process.GetCurrentProcess().Id;
        } catch(Exception e) {
            Diag.Ignore(e);
            return 0;
        }
    }
    private static string[] ForwardedArgs() {
        try {
            string[] all = Environment.GetCommandLineArgs();
            return all is { Length: > 1 } ? all[1..] : [];
        } catch(Exception e) {
            Diag.Ignore(e);
            return [];
        }
    }
    private static string WorkingDir() =>
        method == RestartMethod.Executable && !string.IsNullOrEmpty(launchPath)
            ? Path.GetDirectoryName(launchIsMacBundle ? launchPath.TrimEnd(Path.DirectorySeparatorChar) : launchPath)
            : null;
    private static string UnixScript() {
        int pid = CurrentPid();
        StringBuilder sb = new();
        sb.Append("#!/bin/sh\n");
        if(pid > 0) {
            sb.Append("i=0\n")
                .Append("while kill -0 ").Append(pid.ToString(CultureInfo.InvariantCulture)).Append(" 2>/dev/null; do\n")
                .Append("i=$((i+1))\n")
                .Append("if [ \"$i\" -ge 60 ]; then break; fi\n")
                .Append("sleep 1\n")
                .Append("done\n");
        } else {
            sb.Append("sleep 3\n");
        }
        string cwd = WorkingDir();
        if(!string.IsNullOrEmpty(cwd)) sb.Append("cd ").Append(ShellQuote(cwd)).Append(" || exit 1\n");
        sb.Append(UnixLaunchLine()).Append('\n');
        return sb.ToString();
    }
    private static string UnixLaunchLine() {
        if(method == RestartMethod.Steam) {
            return IsMac
                ? "open " + ShellQuote(steamUrl)
                : "if command -v xdg-open >/dev/null 2>&1; then xdg-open " + ShellQuote(steamUrl)
                    + "; else steam " + ShellQuote(steamUrl) + "; fi";
        }
        string args = string.Join(" ", ForwardedArgs().Select(ShellQuote));
        if(launchIsMacBundle) {
            return "open -n " + ShellQuote(launchPath) + (args.Length == 0 ? "" : " --args " + args);
        }
        return ShellQuote(launchPath) + (args.Length == 0 ? "" : " " + args) + " &";
    }
    private static string WindowsScript() {
        int pid = CurrentPid();
        string pidText = pid.ToString(CultureInfo.InvariantCulture);
        StringBuilder sb = new();
        sb.Append("@echo off\r\n");
        if(pid > 0) {
            sb.Append("set _n=0\r\n")
                .Append(":wait\r\n")
                .Append("tasklist /FI \"PID eq ").Append(pidText).Append("\" /NH 2>nul | find \"")
                .Append(pidText).Append("\" >nul\r\n")
                .Append("if errorlevel 1 goto go\r\n")
                .Append("set /a _n=_n+1\r\n")
                .Append("if %_n% GEQ 60 goto go\r\n")
                .Append("ping -n 2 127.0.0.1 >nul\r\n")
                .Append("goto wait\r\n")
                .Append(":go\r\n");
        } else {
            sb.Append("ping -n 4 127.0.0.1 >nul\r\n");
        }
        sb.Append("if not \"%QUARTZ_RESTART_CWD%\"==\"\" cd /d \"%QUARTZ_RESTART_CWD%\"\r\n")
            .Append("start \"\" \"%QUARTZ_RESTART_TARGET%\" %QUARTZ_RESTART_ARGS%\r\n");
        return sb.ToString();
    }
    private static string ShellQuote(string value) =>
        "'" + (value ?? "").Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    private static string BatchQuote(string value) {
        string clean = (value ?? "").Replace("\"", "", StringComparison.Ordinal);
        return clean.Length != 0 && clean.IndexOf(' ') < 0 ? clean : "\"" + clean + "\"";
    }
}

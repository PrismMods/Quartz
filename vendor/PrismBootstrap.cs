using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

namespace PrismLib.Bootstrap
{
    /* Drop this ONE file into a mod (add it to the csproj <Compile> list) and call
       PrismBootstrap.Ensure(log) first thing in OnLoad. It finds, installs or updates PrismLib and
       makes it loadable — so the user installs Sapphire and gets PrismLib, without a second entry
       in the mod manager.

       Rules this file lives by:

       - It references NO PrismLib type. The CLR resolves a method's types when that method is
         JITted, so the resolver must be installed by a method that cannot possibly mention the
         assembly it is resolving. That is also why the caller must keep its own PrismLib usage in a
         separate [MethodImpl(NoInlining)] method — see Usage in the README.
       - It never throws. Every failure path returns false and the mod runs standalone.
       - It also resolves PrismLib.UI, which mods SHIP in their own folder rather than install.
         The UI half needs no shared instance — two mods each drawing their own toast is correct,
         and ToastStack coordinates them by GameObject name, not shared memory — so it is an
         ordinary dependency and its call sites need no Available guard. The resolver is installed
         before anything else here, so a mod's UI keeps working even when the network half fails.
       - It blocks on the network only when there is no usable copy at all, and then briefly. An
         update to an existing copy is fetched in the background and picked up next launch, because
         a game that hangs on load over a library nobody asked for is worse than being one version
         behind. */
    public static class PrismBootstrap
    {
        // Raw feed. Same shape as a UMM repository entry, minus the parts only UMM reads.
        private const string Feed = "https://raw.githubusercontent.com/PrismMods/PrismLib/main/prismlib.json";
        private const int NetTimeoutMs = 10000;

        private static Action<string> _log;   // null until Ensure hands one over; Say buffers till then
        private static readonly List<string> _early = new List<string>();
        private static bool _ran;
        private static bool _ok;
        private static string _dir;      // the shared PrismLib folder, next to the mod folders
        private static string _root;     // the mods root the mod passed in, if it did
        private static string _bundle;   // the PrismLib.dll copy shipped inside the mod's own zip

        /// What a mod names the PrismLib.dll it ships. Not ".dll": a loader that scans or resolves
        /// DLLs in the mod folder (MelonLoader's UserLibs, UMMBridge) would load it straight from
        /// there, a second PrismLib beside the shared one.
        public const string BundleName = "PrismLib.bundle";

        /* Returns true when PrismLib is present and loadable. Safe to call more than once; only the
           first call's paths count.

           `modsRoot` is the folder that holds the mod folders (UMMMods, Mods, Plugins). Without it
           the root is guessed from where this code was loaded, which goes wrong when the mod is not
           loaded from its own folder: under UMMBridge the guess put PrismLib in steamapps/, and
           Quartz's code runs from a versioned runtime store (…/Runtime/versions/<ver>/).

           `bundle` is the copy the mod ships (default: BundleName beside this code). It is installed
           whenever it is newer than the shared copy, so a mod's first launch needs no network; the
           feed still updates it in the background afterwards, because PrismLib releases more often
           than the mods do. */
        public static bool Ensure(Action<string> log = null, Version minimum = null, string modsRoot = null, string bundle = null)
        {
            if (log != null && _log == null)
            {
                _log = log;
                lock (_early) { foreach (var m in _early) Say(m, true); _early.Clear(); }
            }
            if (_ran) return _ok;
            _ran = true;
            if (!string.IsNullOrEmpty(modsRoot))
            {
                try
                {
                    if (Directory.Exists(modsRoot)) _root = Path.GetFullPath(modsRoot);
                    else Say("mods root " + modsRoot + " does not exist, guessing instead");
                }
                catch (Exception e) { Say("mods root " + modsRoot + " unusable, guessing instead: " + e.Message); }
            }
            _bundle = !string.IsNullOrEmpty(bundle) ? bundle
                    : SelfDir() != null ? Path.Combine(SelfDir(), BundleName) : null;
            SyncUi();
            // First, unconditionally: PrismLib.UI ships beside the mod and must resolve whether or
            // not the shared PrismLib below can be reached.
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try { _ok = Run(minimum ?? new Version(0, 1, 0)); }
            catch (Exception e) { Say("bootstrap failed: " + e.Message); _ok = false; }
            return _ok;
        }

        private static bool Run(Version minimum)
        {
            _dir = SharedDir();
            string path = _dir == null ? null : Path.Combine(_dir, "PrismLib.dll");

            // Another Prism mod may already have loaded it this session. A newer bundle still goes
            // onto disk, so the next launch runs it.
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetName().Name == "PrismLib")
                {
                    Say("already loaded: " + a.GetName().Version);
                    if (path != null) InstallBundled(path);
                    return a.GetName().Version >= minimum;
                }

            if (_dir == null) { Say("no mods folder found"); return false; }
            PromotePending(path);
            InstallBundled(path);

            Version have = VersionOf(path);
            if (have == null || have < minimum)
            {
                Say(have == null ? "not installed, fetching" : "have " + have + ", need " + minimum + ", fetching");
                if (!Install(path)) return false;
                have = VersionOf(path);
                if (have == null || have < minimum) { Say("fetched copy is still too old"); return false; }
            }
            else
            {
                // Good enough to run with. Look for a newer one without making the game wait.
                var t = new Thread(() => { try { UpdateIfNewer(path); } catch { } }) { IsBackground = true };
                t.Start();
            }

            Assembly.LoadFrom(path);
            Say("loaded " + have + " from " + path);
            return true;
        }

        /* PrismLib.UI ships in EVERY mod folder, but only one copy loads per session — whichever the
           loader serves first. Under MelonLoader's UMM bridge that is decided by mod-folder order,
           not version (it registers each folder's DLLs by name and answers AssemblyResolve before we
           can), so an old copy in an alphabetically earlier mod silently wins and a newer mod calling
           a new API throws MissingMethodException.

           So the copies are made to agree instead: the newest version found in any mod folder (or
           UserLibs) replaces every OLDER copy. Never a downgrade, never an equal version (dev builds
           are left alone). Call it first thing in the mod's entry point, before any code that names
           a PrismLib.UI type is JITted — afterwards it can only fix the files for next launch, and
           says so. Writing into a sibling mod's folder is deliberate: it is the same library, only
           ever newer, and the mod's own update simply re-runs this.

           PrismLib.UI stands on Facet (Facet.<major>.<minor>.dll), which ships beside it: a copy
           without one throws FileNotFoundException for every Prism window. So each copy's folder
           also gets every Facet beside the newest copy that it lacks or has older — an upgraded
           copy, and one that came with a mod built before Facet. Never the shared PrismLib folder:
           it holds PrismLib.dll alone.

           Every mods root counts (Mods, UMMMods and Plugins, whichever exist beside each other), so a
           MelonLoader mod's copies and a UMM mod's meet. So do runtime stores, see StoreCopies. */
        public static void SyncUi()
        {
            if (_uiSynced) return;
            _uiSynced = true;
            try
            {
                var copies = new List<string>();
                foreach (var root in Roots())
                    foreach (var d in Subdirs(root))
                    {
                        string p = Path.Combine(d, UiDll);
                        if (!IsShared(d) && File.Exists(p)) copies.Add(p);
                    }
                foreach (var p in StoreCopies()) if (!copies.Contains(p)) copies.Add(p);
                string ul = UserLibs();
                if (ul != null && File.Exists(Path.Combine(ul, UiDll))) copies.Add(Path.Combine(ul, UiDll));

                // Of equal versions, one with Facet beside it: the others may be what needs it.
                string best = null; Version bestV = null;
                foreach (var p in copies)
                {
                    var v = VersionOf(p);
                    if (v != null && (bestV == null || v > bestV || (v == bestV && Facets(best).Count == 0))) { best = p; bestV = v; }
                }
                if (best == null) return;

                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    if (a.GetName().Name == "PrismLib.UI" && a.GetName().Version < bestV)
                        Say("PrismLib.UI " + a.GetName().Version + " already loaded this session; " + bestV + " takes over next launch");

                foreach (var p in copies)
                {
                    var v = VersionOf(p);
                    if (v != null && v < bestV)
                        CopyOver(best, p, "PrismLib.UI " + v + " -> " + bestV + " in " + Where(p));
                }

                foreach (var f in Facets(best))
                {
                    string name = Path.GetFileName(f);
                    Version fv = VersionOf(f);
                    if (fv == null) continue;
                    foreach (var p in copies)
                    {
                        string to = Path.Combine(Path.GetDirectoryName(p), name);
                        Version have = VersionOf(to);
                        if (have == null || have < fv)
                            CopyOver(f, to, name + " " + fv + " -> " + Where(p));
                    }
                }
            }
            catch (Exception e) { Say("PrismLib.UI sync failed: " + e.Message); }
        }

        /* Quartz loads its code from a versioned runtime store instead of a mod folder:
           <mods>/<Mod>/Runtime/versions/<ver>/ under UMM, <game>/UserData/<Mod>/Runtime/versions/<ver>/
           under MelonLoader, and PrismLib.UI ships inside it. A copy there is loaded like any other,
           so it must agree with the rest or "first loaded wins" picks a stale one. The layout is
           Quartz's but the rule is generic: any mod that keeps a store gets the same treatment.
           A version folder without PrismLib.UI.dll is not a copy and is left alone. */
        private static List<string> StoreCopies()
        {
            var outp = new List<string>();
            try
            {
                var parents = Roots();
                if (parents.Count == 0) return outp;
                string game = Path.GetDirectoryName(parents[0]);
                if (game == null) return outp;
                string ud = Path.Combine(game, "UserData");
                if (Directory.Exists(ud)) parents.Add(ud);
                foreach (var parent in parents)
                    foreach (var mod in Subdirs(parent))
                    {
                        string versions = Path.Combine(mod, "Runtime", "versions");
                        if (IsShared(mod) || !Directory.Exists(versions)) continue;
                        foreach (var ver in Subdirs(versions))
                        {
                            if (IsShared(ver)) continue;   // the old Runtime/versions/PrismLib guess
                            string p = Path.Combine(ver, UiDll);
                            if (File.Exists(p) && !outp.Contains(p)) outp.Add(p);
                        }
                    }
            }
            catch (Exception e) { Say("runtime store scan failed: " + e.Message); }
            return outp;
        }

        // A root that cannot be listed (permissions) is skipped and logged once; the others still count.
        private static readonly HashSet<string> _badDirs = new HashSet<string>();
        private static string[] Subdirs(string dir)
        {
            try { return Directory.GetDirectories(dir); }
            catch (Exception e)
            {
                if (_badDirs.Add(dir)) Say("cannot list " + dir + ", skipped: " + e.Message);
                return new string[0];
            }
        }

        // Folder name for logs; a runtime-store version folder is named with its mod (Q/1.0.0).
        private static string Where(string file)
        {
            string dir = Path.GetDirectoryName(file);
            string name = Path.GetFileName(dir);
            string versions = Path.GetDirectoryName(dir);
            if (versions != null && Path.GetFileName(versions) == "versions")
            {
                string mod = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(versions)) ?? "");
                if (mod.Length > 0) return mod + "/" + name;
            }
            return name;
        }

        // The mods root, then its sibling (Mods beside UMMMods) when both exist: one game, two loaders.
        private static List<string> Roots()
        {
            var outp = new List<string>();
            string root = ModsRoot();
            if (root == null) return outp;
            outp.Add(root);
            string game = Path.GetDirectoryName(root);
            if (game != null)
                foreach (var n in ModRootNames)
                {
                    string d = Path.Combine(game, n);
                    if (Directory.Exists(d) && !string.Equals(Path.GetFullPath(d), root, StringComparison.OrdinalIgnoreCase)) outp.Add(d);
                }
            return outp;
        }

        // Per file: on Windows a copy another mod already loaded is locked, and that must not stop
        // the rest. It gets replaced on a later launch, when this runs first.
        private static void CopyOver(string from, string to, string done)
        {
            string tmp = to + ".new";
            try
            {
                File.Copy(from, tmp, true);
                if (File.Exists(to)) File.Delete(to);
                File.Move(tmp, to);
                Say(done);
            }
            catch (Exception e)
            {
                try { File.Delete(tmp); } catch { }
                Say(Path.GetFileName(to) + " in " + Where(to) + " in use, next launch: " + e.Message);
            }
        }

        private const string UiDll = "PrismLib.UI.dll";
        private static bool _uiSynced;

        // Facet.<major>.<minor>: the engine itself, not a Facet-named mod (Facet.Mod).
        private static bool IsFacet(string name)
        {
            return Regex.IsMatch(name, @"^Facet\.[0-9]+\.[0-9]+$");
        }

        // The Facet DLLs beside a PrismLib.UI copy.
        private static List<string> Facets(string uiCopy)
        {
            var outp = new List<string>();
            foreach (var f in Directory.GetFiles(Path.GetDirectoryName(uiCopy), "Facet.*.dll"))
                if (IsFacet(Path.GetFileNameWithoutExtension(f))) outp.Add(f);
            return outp;
        }

        // The shared folder (SharedDir) holds PrismLib.dll alone; nothing else is read from or put there.
        private static bool IsShared(string dir)
        {
            return string.Equals(Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar)), "PrismLib", StringComparison.OrdinalIgnoreCase);
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string want = new AssemblyName(args.Name).Name;
                bool facet = IsFacet(want);
                if (!facet && !want.StartsWith("PrismLib", StringComparison.OrdinalIgnoreCase)) return null;

                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    if (string.Equals(a.GetName().Name, want, StringComparison.OrdinalIgnoreCase)) return a;

                // Beside the mod first (a shipped PrismLib.UI), then the shared folder (the
                // installed PrismLib), then UserLibs (where a MelonLoader build puts them).
                // Facet: beside the mod, UserLibs, then any sibling mod's folder or runtime store — whichever mod's
                // PrismLib.UI loaded may have come without one (SyncUi, a mod built before Facet).
                // The cheap places first, so a lookup never pays for a scan (or fails on a bad root) when
                // they have it.
                var dirs = new List<string> { SelfDir() };
                if (!facet) dirs.Add(_dir);
                dirs.Add(UserLibs());
                Assembly found = LoadFirst(dirs, want);
                if (found != null || !facet) return found;

                dirs.Clear();
                foreach (var root in Roots())
                    foreach (var d in Subdirs(root))
                        if (!IsShared(d)) dirs.Add(d);
                foreach (var c in StoreCopies()) dirs.Add(Path.GetDirectoryName(c));
                return LoadFirst(dirs, want);
            }
            catch { return null; }
        }

        private static Assembly LoadFirst(List<string> dirs, string want)
        {
            foreach (var dir in dirs)
            {
                if (dir == null) continue;
                string p = Path.Combine(dir, want + ".dll");
                if (File.Exists(p)) return Assembly.LoadFrom(p);
            }
            return null;
        }
        private static string SelfDir()
        {
            try { return Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath); }
            catch { return null; }
        }

        /* Where the shared copy lives, for BOTH loaders.

           UMM gives each mod a folder: <game>/UMMMods/<Mod>/<Mod>.dll, so the mods root is the
           parent of the parent. MelonLoader does not — its mods are flat DLLs in <game>/Mods —
           so the same walk lands on the game root and scatters a PrismLib folder there. Decide by
           looking at the directory name rather than assuming a depth. */
        private static readonly string[] ModRootNames = { "Mods", "UMMMods", "Plugins" };

        private static string ModsRoot()
        {
            if (_root != null) return _root;
            try
            {
                string self = SelfDir();
                if (self == null) return null;
                string root = IsModRoot(self) ? self : Path.GetDirectoryName(self);
                return root != null && Directory.Exists(root) ? root : null;
            }
            catch { return null; }
        }

        private static string SharedDir()
        {
            try
            {
                string modsRoot = ModsRoot();
                if (modsRoot == null) return null;
                string dir = Path.Combine(modsRoot, "PrismLib");
                Directory.CreateDirectory(dir);
                return dir;
            }
            catch { return null; }
        }

        private static bool IsModRoot(string dir)
        {
            string name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar));
            foreach (var n in ModRootNames)
                if (string.Equals(name, n, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /* UserLibs is where both loaders keep shared assemblies, and MelonLoader loads them
           itself. A MelonLoader build of a mod ships PrismLib.UI there rather than into Mods,
           where MelonLoader would scan it for a MelonMod and complain that it has none. */
        private static string UserLibs()
        {
            try
            {
                string mods = ModsRoot();
                string root = mods == null ? null : Path.GetDirectoryName(mods);
                if (root == null) return null;
                string dir = Path.Combine(root, "UserLibs");
                return Directory.Exists(dir) ? dir : null;
            }
            catch { return null; }
        }

        private static Version VersionOf(string path)
        {
            // Metadata-only read: does not load the assembly, so a stale copy can be overwritten.
            try { return File.Exists(path) ? AssemblyName.GetAssemblyName(path).Version : null; }
            catch { return null; }
        }

        private static void UpdateIfNewer(string path)
        {
            // Quiet: a background update that cannot reach GitHub is not actionable — the mod is
            // already running on a good copy — and the thread is aborted outright at shutdown.
            var feed = Fetch(true);
            if (feed == null) return;
            var have = VersionOf(path);
            if (have != null && have >= feed.Version) return;
            Say("update available: " + feed.Version + " (active next launch)");
            Download(feed, path);
        }

        private static bool Install(string path)
        {
            var feed = Fetch();
            if (feed == null) { Say("feed unreachable"); return false; }
            return Download(feed, path);
        }

        /* Verified before it lands: the bytes are hashed in memory and only written once they match
           the feed's sha256. A wrong hash means the file is discarded, never loaded — this is code
           that will execute inside the game. */
        private static bool Download(FeedEntry feed, string path)
        {
            try
            {
                byte[] dll = Get(feed.Url);
                if (dll == null || dll.Length == 0) { Say("download failed"); return false; }
                string got;
                using (var sha = SHA256.Create())
                    got = BitConverter.ToString(sha.ComputeHash(dll)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(got, feed.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    Say("SHA-256 mismatch, discarding (want " + feed.Sha256 + ", got " + got + ")");
                    return false;
                }
                return Place(dll, path, feed.Version.ToString());
            }
            catch (Exception e) { Say("install failed: " + e.Message); return false; }
        }

        /* The mod's own zip is as trusted as the mod, so no hash. Compared against the pending copy
           too, so an older bundle never overwrites a newer download waiting for the next launch. */
        private static void InstallBundled(string path)
        {
            try
            {
                if (_bundle == null || !File.Exists(_bundle)) return;
                Version b = VersionOf(_bundle), have = VersionOf(path), pending = VersionOf(path + ".new");
                if (b == null || (have != null && b <= have) || (pending != null && b <= pending)) return;
                Place(File.ReadAllBytes(_bundle), path, b + " (bundled)");
            }
            catch (Exception e) { Say("bundled copy not installed: " + e.Message); }
        }

        // Written beside, then swapped in. A loaded DLL cannot be deleted on Windows, so there it
        // stays as .new and PromotePending takes it on the next launch.
        private static bool Place(byte[] dll, string path, string what)
        {
            string tmp = path + ".new";
            File.WriteAllBytes(tmp, dll);
            try
            {
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Say("installed " + what + " as pending (" + e.Message + ")");
                return false;
            }
            Say("installed " + what);
            return true;
        }

        /* Windows will not delete a DLL the process has loaded, and the background update runs right
           after Run loaded this one — so there the new copy stays as .new (already hash-checked) and
           takes over here on the next launch, before anything has loaded the old one. */
        private static void PromotePending(string path)
        {
            string pending = path + ".new";
            if (!File.Exists(pending)) return;
            try
            {
                Version pv = VersionOf(pending), hv = VersionOf(path);
                if (pv != null && (hv == null || pv > hv))
                {
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(pending, path);
                    Say("activated pending " + pv);
                }
                else File.Delete(pending);
            }
            catch (Exception e) { Say("pending update not activated: " + e.Message); }
        }

        private sealed class FeedEntry { public Version Version; public string Url; public string Sha256; }

        private static FeedEntry Fetch(bool quiet = false)
        {
            try
            {
                byte[] raw = Get(Feed, quiet);
                if (raw == null) return null;
                string json = System.Text.Encoding.UTF8.GetString(raw);
                // Three flat string fields; a JSON dependency would be the only reason this file
                // could not be dropped into any mod as-is.
                string v = Field(json, "version"), u = Field(json, "url"), s = Field(json, "sha256");
                Version ver;
                if (v == null || u == null || s == null || !Version.TryParse(v.Split('-')[0], out ver)) return null;
                return new FeedEntry { Version = ver, Url = u, Sha256 = s };
            }
            catch { return null; }
        }

        private static string Field(string json, string key)
        {
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static byte[] Get(string url, bool quiet = false)
        {
            try
            {
                // Mono's default protocol list predates GitHub dropping everything below TLS 1.2.
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = req.ReadWriteTimeout = NetTimeoutMs;
                req.UserAgent = "PrismBootstrap";
                using (var res = req.GetResponse())
                using (var s = res.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    var buf = new byte[16384];
                    int n;
                    while ((n = s.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
                    return ms.ToArray();
                }
            }
            catch (Exception e) { if (!quiet) Say("GET " + url + " failed: " + e.Message); return null; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Say(string msg, bool flushing = false)
        {
            if (_log == null) { lock (_early) _early.Add(msg); return; }
            try { _log("PrismLib bootstrap: " + msg); } catch { }
            if (!flushing) Debug.WriteLine("PrismLib bootstrap: " + msg);
        }
    }
}

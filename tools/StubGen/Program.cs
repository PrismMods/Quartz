using Mono.Cecil;
using System.Text;
using System.Text.Json;

namespace Quartz.StubGen;

static class Program {
    /// <summary>
    /// Assemblies the stub set deliberately does NOT stand in for, because a
    /// netstandard2.1 compilation already has them. Anything else found beside the
    /// game gets stubbed, so a new game dependency shows up as a new stub rather than
    /// as a silent compile break in CI.
    /// </summary>
    static readonly HashSet<string> Bcl = new(StringComparer.OrdinalIgnoreCase) {
        "mscorlib", "netstandard", "System", "System.Core", "System.Runtime", "System.Data",
        "System.Xml", "System.Xml.Linq", "System.Numerics", "System.Buffers", "System.Memory",
        "System.ValueTuple", "System.IO.Compression", "System.IO.Compression.FileSystem",
        "System.Collections.Immutable", "System.ComponentModel.Composition",
        "System.ComponentModel.DataAnnotations", "System.Runtime.Serialization",
        "System.ServiceModel.Internals", "System.Threading.Tasks.Extensions",
        "System.Drawing", "System.Drawing.Primitives", "System.Net.Http", "System.Net.Http.WebRequest",
        "System.Runtime.CompilerServices.Unsafe", "Microsoft.Bcl.AsyncInterfaces",
        "Microsoft.Bcl.TimeProvider", "Mono.Security", "Mono.Posix",
    };

    static int Main(string[] args) {
        string game = Arg(args, "--game");
        string outDir = Arg(args, "--out");
        bool check = args.Contains("--check");
        List<string> inputs = All(args, "--input");
        List<string> libs = All(args, "--lib");

        if(game == null || outDir == null || inputs.Count == 0) {
            Console.Error.WriteLine(
                "usage: StubGen --game <Managed dir> [--lib <dir>]... [--source <dir>]... --input <built dll> [--input ...] --out <stubs dir> [--check]");
            return 2;
        }

        DefaultAssemblyResolver resolver = new();
        resolver.AddSearchDirectory(game);
        foreach(string lib in libs) if(Directory.Exists(lib)) resolver.AddSearchDirectory(lib);
        foreach(string input in inputs) resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(input)));
        ReaderParameters read = new() { AssemblyResolver = resolver };

        HashSet<string> targets = Discover(game, libs);
        Dictionary<string, HashSet<string>> sourceTargets = SourceScan.Targets(All(args, "--source"));
        Console.WriteLine($"source scan: {sourceTargets.Count} types named from nameof/Harmony strings"
            + $" ({sourceTargets.Values.Sum(v => v.Count)} members)");
        Collector collector = new(targets, sourceTargets);
        List<AssemblyDefinition> opened = new();
        foreach(string input in inputs) {
            AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(input, read);
            opened.Add(asm);
            collector.Add(asm.MainModule);
        }
        foreach(string name in ProjectScan.ReferencedAssemblies(All(args, "--source"))) collector.Require(name);
        collector.Close();

        List<AssemblyDefinition> gameAssemblies = new();
        foreach(string name in targets) {
            string path = new[] { game }.Concat(libs)
                .Select(d => Path.Combine(d, name + ".dll")).FirstOrDefault(File.Exists);
            if(path == null) continue;
            try { gameAssemblies.Add(AssemblyDefinition.ReadAssembly(path, read)); } catch { }
        }
        List<PatchTargets.Row> patchTargets = PatchTargets.Resolve(sourceTargets, collector.Output, gameAssemblies);
        int broken = patchTargets.Count(r => !r.Resolved);

        Dictionary<string, string> files = new(StringComparer.Ordinal);
        int types = 0, members = 0;
        foreach(AssemblyReq asm in collector.Output.Values.OrderBy(a => a.Name, StringComparer.Ordinal)) {
            files[Path.Combine(asm.Name, "Stubs.g.cs")] = Emitter.Render(asm, collector.ExternallyNamed, Header(asm.Name));
            files[Path.Combine(asm.Name, asm.Name + ".csproj")] = Project(asm);
            types += asm.Types.Count;
            foreach(TypeReq t in asm.Types.Values)
                members += t.Methods.Count + t.Fields.Count + t.Properties.Count + t.Events.Count;
        }
        files["Stubs.proj"] = Solution(collector.Output.Values.Select(a => a.Name));
        files["PATCH-TARGETS.json"] = PatchTargets.Render(patchTargets);
        files["MANIFEST.json"] = Manifest(collector.Output.Values);
        files["README.md"] = Readme(collector.Output.Values, types, members);

        foreach(AssemblyDefinition asm in opened.Concat(gameAssemblies)) asm.Dispose();

        Console.WriteLine($"{collector.Output.Count} assemblies, {types} types, {members} members");
        Console.WriteLine($"patch targets: {patchTargets.Count} checked, {broken} unresolved");
        foreach(PatchTargets.Row row in patchTargets.Where(r => !r.Resolved))
            Console.Error.WriteLine($"  BROKEN PATCH TARGET: {row.Type}.{row.Member} no longer exists");

        int status = check ? Check(outDir, files) : Write(outDir, files);
        // A patch naming a member the game dropped is worth failing over even when
        // everything else is in order: it compiles, ships, and dies silently at JIT time.
        if(broken > 0) {
            Console.Error.WriteLine($"\nFAIL: {broken} Harmony patch target(s) do not exist in the installed game.");
            return 1;
        }
        return status;
    }

    /// <summary>Every non-BCL assembly sitting beside the game is a stub candidate.</summary>
    static HashSet<string> Discover(string game, List<string> libs) {
        HashSet<string> found = new(StringComparer.OrdinalIgnoreCase);
        foreach(string dir in libs.Prepend(game)) {
            if(!Directory.Exists(dir)) continue;
            foreach(string dll in Directory.EnumerateFiles(dir, "*.dll")) {
                string name = Path.GetFileNameWithoutExtension(dll);
                if(Bcl.Contains(name)) continue;
                if(name.StartsWith("I18N", StringComparison.OrdinalIgnoreCase)) continue;
                found.Add(name);
            }
        }
        return found;
    }

    static bool Generated(string relative) {
        string[] parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !parts.Contains("bin") && !parts.Contains("obj");
    }

    static int Write(string outDir, Dictionary<string, string> files) {
        if(Directory.Exists(outDir)) {
            foreach(string stale in Directory.EnumerateFiles(outDir, "*", SearchOption.AllDirectories)) {
                string relative = Path.GetRelativePath(outDir, stale);
                if(Generated(relative) && !files.ContainsKey(relative)) File.Delete(stale);
            }
        }
        foreach((string relative, string text) in files) {
            string path = Path.Combine(outDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if(File.Exists(path) && File.ReadAllText(path) == text) continue;
            File.WriteAllText(path, text);
        }
        foreach(string dir in Directory.EnumerateDirectories(outDir).Where(d => !Directory.EnumerateFileSystemEntries(d).Any()))
            Directory.Delete(dir);
        Console.WriteLine($"wrote {files.Count} files to {outDir}");
        return 0;
    }

    /// <summary>
    /// Fails when the committed stubs no longer describe the installed game. This is
    /// the drift gate: the stubs are what CI compiles against, so if they and the real
    /// assemblies disagree, CI is proving something about an API that no longer exists.
    /// </summary>
    static int Check(string outDir, Dictionary<string, string> files) {
        List<string> problems = new();
        foreach((string relative, string text) in files) {
            string path = Path.Combine(outDir, relative);
            if(!File.Exists(path)) { problems.Add("missing:  " + relative); continue; }
            if(File.ReadAllText(path) != text) problems.Add("outdated: " + relative);
        }
        if(Directory.Exists(outDir))
            foreach(string existing in Directory.EnumerateFiles(outDir, "*", SearchOption.AllDirectories)) {
                string relative = Path.GetRelativePath(outDir, existing);
                if(Generated(relative) && !files.ContainsKey(relative)) problems.Add("stale:    " + relative);
            }
        if(problems.Count == 0) {
            Console.WriteLine("ok: committed stubs match the installed game");
            return 0;
        }
        Console.Error.WriteLine("FAIL: stubs are out of date with the installed game");
        foreach(string p in problems.OrderBy(x => x, StringComparer.Ordinal)) Console.Error.WriteLine("  " + p);
        Console.Error.WriteLine("\n  regenerate with: tools/gen-stubs.sh");
        return 1;
    }

    // --- generated file bodies ------------------------------------------------

    static string Header(string assembly) =>
        $"""
        // <auto-generated>
        //   Compile-only stand-in for "{assembly}", generated by tools/StubGen.
        //
        //   Every declaration below exists because Quartz's own compiled IL names it.
        //   The set is derived from OUR usage, not copied from the game: no method
        //   bodies, no constants beyond the ones our code reads, no resources, no
        //   types we never touch. It exists so CI can compile the whole mod without a
        //   game install, and it is never shipped or loaded at runtime.
        //
        //   Do not edit by hand. Run tools/gen-stubs.sh against a real install.
        // </auto-generated>

        """;

    static string Project(AssemblyReq asm) {
        StringBuilder references = new();
        foreach(string dependency in asm.DependsOn.OrderBy(d => d, StringComparer.Ordinal))
            references.Append($"\t\t<ProjectReference Include=\"../{dependency}/{dependency}.csproj\" />\n");
        return $"""
        <Project Sdk="Microsoft.NET.Sdk">
        <!-- Generated by tools/StubGen. Do not edit. -->
        	<PropertyGroup>
        		<TargetFramework>netstandard2.1</TargetFramework>
        		<AssemblyName>{asm.Name}</AssemblyName>
        		<LangVersion>latest</LangVersion>
        		<OutputPath>$(MSBuildThisFileDirectory)../bin/</OutputPath>
        		<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
        		<RootNamespace></RootNamespace>
        		<EnableDefaultCompileItems>false</EnableDefaultCompileItems>
        		<ImplicitUsings>false</ImplicitUsings>
        		<Nullable>disable</Nullable>
        		<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
        		<GenerateDocumentationFile>false</GenerateDocumentationFile>
        		<EnableNETAnalyzers>false</EnableNETAnalyzers>
        		<IsPackable>false</IsPackable>
        		<NoWarn>$(NoWarn);CS0108;CS0109;CS0114;CS0465;CS0824;CS1591;CS8981;CS0169;CS0649;CS0067</NoWarn>
        		<ProduceReferenceAssembly>false</ProduceReferenceAssembly>
        		<Deterministic>true</Deterministic>
        	</PropertyGroup>
        	<ItemGroup>
        		<Compile Include="Stubs.g.cs" />
        	</ItemGroup>
        	<ItemGroup>
        {references}	</ItemGroup>
        </Project>

        """;
    }

    /// <summary>
    /// A traversal project, not an SDK project with references: the stub assemblies are
    /// the deliverable, and an SDK project with no sources of its own cannot build.
    /// </summary>
    static string Solution(IEnumerable<string> names) {
        StringBuilder items = new();
        foreach(string name in names.OrderBy(n => n, StringComparer.Ordinal))
            items.Append($"\t\t<StubProject Include=\"$(MSBuildThisFileDirectory){name}/{name}.csproj\" />\n");
        return $"""
        <Project DefaultTargets="Build">
        <!-- Generated by tools/StubGen. Builds every stub assembly into stubs/bin. -->
        	<PropertyGroup>
        		<Configuration Condition="'$(Configuration)' == ''">Release</Configuration>
        	</PropertyGroup>
        	<ItemGroup>
        {items}	</ItemGroup>
        	<Target Name="Build">
        		<MSBuild Projects="@(StubProject)" Targets="Build" Properties="Configuration=$(Configuration)" BuildInParallel="true" />
        	</Target>
        	<Target Name="Restore">
        		<MSBuild Projects="@(StubProject)" Targets="Restore" Properties="Configuration=$(Configuration)" />
        	</Target>
        	<Target Name="Clean">
        		<MSBuild Projects="@(StubProject)" Targets="Clean" Properties="Configuration=$(Configuration)" />
        	</Target>
        </Project>

        """;
    }

    static string Manifest(IEnumerable<AssemblyReq> assemblies) {
        var model = assemblies.OrderBy(a => a.Name, StringComparer.Ordinal).Select(a => new {
            assembly = a.Name,
            types = a.Types.Values.OrderBy(t => t.Def.FullName, StringComparer.Ordinal).Select(t => new {
                type = t.Def.FullName,
                members = t.Methods.Select(m => m.FullName)
                    .Concat(t.Fields.Select(f => f.FullName))
                    .Concat(t.Properties.Select(p => p.FullName))
                    .Concat(t.Events.Select(e => e.FullName))
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            }).ToArray(),
        });
        return JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    static string Readme(IEnumerable<AssemblyReq> assemblies, int types, int members) {
        StringBuilder rows = new();
        foreach(AssemblyReq a in assemblies.OrderBy(a => a.Name, StringComparer.Ordinal)) {
            int m = a.Types.Values.Sum(t => t.Methods.Count + t.Fields.Count + t.Properties.Count + t.Events.Count);
            rows.Append($"| `{a.Name}` | {a.Types.Count} | {m} |\n");
        }
        return $"""
        # Generated stubs

        <!-- Generated by tools/StubGen. Do not edit. -->

        Compile-only stand-ins for the assemblies Quartz links against, so CI can build
        every source file without a game install. **Nothing here ships**: the mod always
        binds to the real assemblies at runtime, and these projects are never packaged.

        Each declaration exists because Quartz's own compiled IL names it. The set is
        derived from our usage: `tools/StubGen` reads the built `Quartz.dll`, `QuartzUmm.dll`
        and every `Quartz.Module.*.dll`, walks their metadata reference tables, and emits
        exactly the types and members found there plus whatever their signatures drag in.
        It also scans the sources for `nameof` and Harmony string literals, since those are
        compile-time only and leave no reference to follow. There are no method bodies, no
        resources, and no types the mod never touches.

        | Assembly | Types | Members |
        |---|---:|---:|
        {rows}
        **{types} types, {members} members.**

        ## How it is used

        `-p:UseStubs=true` points every game `HintPath` here (see `build/Stubs.props`), so
        the CI `compile` job builds the core mod on both loader targets plus every module
        with `TreatWarningsAsErrors` and no game install:

        ```bash
        dotnet build stubs/Stubs.proj -c Release
        dotnet build Quartz/Quartz.csproj -c Release -p:UseStubs=true -p:GamePath= -p:GameData=
        dotnet build modules/AllModules.proj -c Release -p:UseStubs=true -p:GamePath= -p:GameData=
        ```

        ## Regenerating

        Needs a real game install (located the same way `build.sh` does it):

        ```bash
        tools/gen-stubs.sh
        ```

        ## Keeping it honest

        Stubs that disagree with the game would make CI green about an API that no longer
        exists, so drift is caught from both directions:

        - `tools/gen-stubs.sh --check` regenerates and fails on any difference. It needs
          the game, so `tools/release.sh` runs it as a preflight — a release can never
          ship past a stale stub set.
        - `PATCH-TARGETS.json` records every game member named by a `[HarmonyPatch]` or a
          `nameof`, resolved against the real assemblies. Those names are strings, so
          nothing else can check them — and a patch pointing at a member the game dropped
          makes Mono discard the whole patched method at JIT time, silently.
          `scripts/check_conventions.py` fails on an unresolved entry, and needs no game.

        """;
    }

    // --- arg parsing ----------------------------------------------------------

    static string Arg(string[] args, string name) {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static List<string> All(string[] args, string name) {
        List<string> values = new();
        for(int i = 0; i < args.Length - 1; i++)
            if(args[i] == name) values.Add(args[i + 1]);
        return values;
    }
}

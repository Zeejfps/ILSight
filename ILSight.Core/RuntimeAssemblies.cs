using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace ILSight.Core;

/// <summary>Finding what <see cref="InstalledRuntime"/> holds, and reading it.</summary>
public static class RuntimeAssemblies
{
    /// <summary>The runtime's own directory first, then the sibling shared frameworks of the same
    /// version (ASP.NET Core, Windows Desktop) that are installed.</summary>
    public static IReadOnlyList<string> ListDirectories(InstalledRuntime runtime)
    {
        return runtime.DirectoryPaths.Value;
    }

    /// <summary>Every managed assembly in <see cref="ListDirectories"/>, sorted by name. Scanned once, on
    /// first use.</summary>
    public static IReadOnlyList<RuntimeAssembly> ListAssemblies(InstalledRuntime runtime)
    {
        return runtime.Assemblies.Value;
    }

    /// <summary>The runtime's file of the assembly named <paramref name="simpleName"/>, or null.</summary>
    /// <exception cref="IOException">The runtime's directories cannot be listed for now.</exception>
    internal static string? Find(InstalledRuntime runtime, string simpleName)
    {
        return runtime.FilesByName.Value.GetValueOrDefault(simpleName);
    }

    /// <summary>The newest <c>Microsoft.NETCore.App</c> directory under the first of
    /// <paramref name="dotnetRoots"/> that has one, preferring version <paramref name="major"/>.</summary>
    internal static string? FindInstalledRuntime(IEnumerable<string> dotnetRoots, int major)
    {
        foreach (var root in dotnetRoots)
        {
            var frameworks = Path.Combine(root, "shared", "Microsoft.NETCore.App");
            if (!Directory.Exists(frameworks))
            {
                continue;
            }

            var versions = new List<(string Dir, Version Version)>();
            foreach (var dir in Directory.EnumerateDirectories(frameworks))
            {
                var version = ParseVersion(Path.GetFileName(dir));
                if (version is null)
                {
                    continue;
                }

                var coreLib = Path.Combine(dir, "System.Private.CoreLib.dll");
                if (File.Exists(coreLib))
                {
                    versions.Add((dir, version));
                }
            }
            var best = versions.OrderByDescending(d => d.Version.Major == major).ThenByDescending(d => d.Version).Select(d => d.Dir).FirstOrDefault();
            if (best is not null)
            {
                return best;
            }
        }
        return null;
    }

    private static string RuntimeDirectory()
    {
        var running = Path.TrimEndingDirectorySeparator(RuntimeEnvironment.GetRuntimeDirectory());
        var coreLib = Path.Combine(running, "System.Private.CoreLib.dll");
        if (File.Exists(coreLib))
        {
            return running;
        }

        return FindInstalledRuntime(DotNetRoots(), Environment.Version.Major) ?? running;
    }

    internal static IEnumerable<string> DotNetRoots()
    {
        var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(root))
        {
            yield return root;
        }

        var exe = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = new FileInfo(Path.Combine(dir, exe));
            if (!candidate.Exists)
            {
                continue;
            }

            var target = candidate.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? candidate.FullName;
            var resolved = Path.GetDirectoryName(target);
            if (resolved is not null)
            {
                yield return resolved;
            }
        }
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/usr/local/share/dotnet";
        }
        else
        {
            yield return "/usr/share/dotnet";
            yield return "/usr/lib/dotnet";
        }
    }

    internal static ImmutableArray<(string Framework, string Path)> FindDirectories()
    {
        var runtime = RuntimeDirectory();
        var frameworkDir = Path.GetDirectoryName(runtime);
        var sharedDir = frameworkDir is null ? null : Path.GetDirectoryName(frameworkDir);
        var result = ImmutableArray.CreateBuilder<(string, string)>();
        result.Add((frameworkDir is null ? "Microsoft.NETCore.App" : Path.GetFileName(frameworkDir), runtime));
        if (sharedDir is null || frameworkDir is null || !Directory.Exists(sharedDir))
        {
            return result.ToImmutable();
        }

        var version = ParseVersion(Path.GetFileName(runtime));
        foreach (var sibling in Directory.EnumerateDirectories(sharedDir).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(sibling, frameworkDir, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var best = Directory.EnumerateDirectories(sibling)
                .Select(dir => (Dir: dir, Version: ParseVersion(Path.GetFileName(dir))))
                .Where(d => d.Version is not null && version is not null && d.Version.Major == version.Major && d.Version.Minor == version.Minor)
                .MaxBy(d => d.Version);
            if (best.Dir is not null)
            {
                result.Add((Path.GetFileName(sibling), best.Dir));
            }
        }
        return result.ToImmutable();
    }

    private static Version? ParseVersion(string name)
    {
        var dash = name.IndexOf('-');
        return Version.TryParse(dash < 0 ? name : name[..dash], out var version) ? version : null;
    }

    internal static IReadOnlyDictionary<string, string> IndexFiles(InstalledRuntime runtime)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, dir) in runtime.Directories.Value)
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.dll"))
            {
                result.TryAdd(Path.GetFileNameWithoutExtension(file), file);
            }
        }

        return result;
    }

    internal static ImmutableArray<RuntimeAssembly> ScanAssemblies(InstalledRuntime runtime)
    {
        var result = new List<RuntimeAssembly>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (framework, dir) in runtime.Directories.Value)
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (seen.Contains(name) || !IsManagedAssembly(file))
                {
                    continue;
                }

                var path = AssemblyPath.Parse(file);
            if (path is null)
                {
                    continue;
                }

                seen.Add(name);
            result.Add(new RuntimeAssembly(name, framework, path));
        }
        }

        result.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        return result.ToImmutableArray();
    }

    private static bool IsManagedAssembly(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new PEReader(stream);
            return reader.HasMetadata && reader.GetMetadataReader().IsAssembly;
        }
        catch (Exception e) when (e is IOException or BadImageFormatException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

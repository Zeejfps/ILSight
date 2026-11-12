using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace ILSight.Core;

/// <summary>Finding where .NET is installed on this machine and what its shared frameworks hold: the environment and
/// the disk, I/O which only the composition root and the app's adapters call (listed in
/// <c>rulelab.ambient_types</c>).</summary>
public static class RuntimeFolders
{
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

    /// <summary>The running runtime's shared framework directory first, then the sibling shared frameworks of the same
    /// version (ASP.NET Core, Windows Desktop) that are installed; for a native build, which runs on no shared
    /// framework, those of the newest installed runtime.</summary>
    public static ImmutableArray<(string Framework, string Path)> FindDirectories()
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

    /// <summary>The assembly files in <paramref name="directories"/>, by simple name, the first directory's
    /// first; a directory that cannot be listed gives none.</summary>
    public static IReadOnlyDictionary<string, string> IndexFiles(ImmutableArray<(string Framework, string Path)> directories)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, dir) in directories)
        {
            foreach (var file in ListAssemblyFiles(dir))
            {
                result.TryAdd(Path.GetFileNameWithoutExtension(file), file);
            }
        }

        return result;
    }

    private static List<string> ListAssemblyFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*.dll").ToList();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Every managed assembly in <paramref name="directories"/>, sorted by name.</summary>
    public static IReadOnlyList<RuntimeAssembly> ScanAssemblies(ImmutableArray<(string Framework, string Path)> directories)
    {
        var result = new List<RuntimeAssembly>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (framework, dir) in directories)
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
        return result;
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

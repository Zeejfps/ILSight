using System.Collections.Immutable;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core;

/// <summary>The ready-made assembly lists this machine offers: one per installed shared framework and
/// feature version, of its newest patch (<c>Microsoft.NETCore.App 9.0.3</c>), newest first, then on
/// Windows the .NET Framework lists found in the GAC.</summary>
public static class AssemblyListPresets
{
    private const string SharedDirectoryName = "shared";
    private const string ReferencePackSuffix = ".Ref";
    private const string NativeHelperSuffix = "_cor3.dll";
    private const string NativeLibraryInfix = ".Native.";

    private static readonly HashSet<string> LowerCaseAssemblies = new(StringComparer.OrdinalIgnoreCase) { "mscorlib.dll", "netstandard.dll" };

    /// <summary>The presets this machine offers, read from the installed frameworks' folders and the GAC.</summary>
    public static IReadOnlyList<AssemblyListPreset> FindInstalled()
    {
        var presets = new List<AssemblyListPreset>();
        var root = FindDotNetRoot();
        if (root is not null)
        {
            presets.AddRange(FindRuntimePresets(root));
        }

        presets.AddRange(FindGacPresets());
        return presets;
    }

    private static string? FindDotNetRoot()
    {
        return RuntimeFolders.DotNetRoots()
            .FirstOrDefault(HasSharedFrameworks);
    }

    private static bool HasSharedFrameworks(string root)
    {
        return Directory.Exists(Path.Combine(root, SharedDirectoryName));
    }

    /// <summary>A preset per shared framework under <paramref name="dotnetRoot"/> and feature version,
    /// of its newest patch, newest first. Versions holding no assemblies are left out.</summary>
    internal static List<AssemblyListPreset> FindRuntimePresets(string dotnetRoot)
    {
        var presets = new List<AssemblyListPreset>();
        if (!HasSharedFrameworks(dotnetRoot))
        {
            return presets;
        }

        var frameworks = Directory.EnumerateDirectories(Path.Combine(dotnetRoot, SharedDirectoryName))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var framework in frameworks)
        {
            presets.AddRange(FindFrameworkPresets(framework));
        }

        return presets;
    }

    private static List<AssemblyListPreset> FindFrameworkPresets(string frameworkDirectory)
    {
        var presets = new List<AssemblyListPreset>();
        var framework = Path.GetFileName(frameworkDirectory);
        if (framework.EndsWith(ReferencePackSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return presets;
        }

        foreach (var version in ListNewestPatches(frameworkDirectory))
        {
            var paths = ListFrameworkAssemblies(version.Directory);
            if (paths.Count == 0)
            {
                continue;
            }

            presets.Add(new AssemblyListPreset { Name = $"{framework} {version.Label}", Paths = PathsOf(paths) });
        }
        return presets;
    }

    /// <summary>The newest patch of each feature version installed in <paramref name="frameworkDirectory"/>, newest first.</summary>
    private static List<InstalledVersion> ListNewestPatches(string frameworkDirectory)
    {
        var newest = new Dictionary<(int Major, int Minor), InstalledVersion>();
        foreach (var directory in Directory.EnumerateDirectories(frameworkDirectory))
        {
            var version = ReadVersion(directory);
            if (version is null)
            {
                continue;
            }

            var feature = (version.Number.Major, version.Number.Minor);
            var kept = newest.GetValueOrDefault(feature);
            if (kept is null || version.Number > kept.Number)
            {
                newest[feature] = version;
            }
        }
        return newest.Values
            .OrderByDescending(v => v.Number)
            .ToList();
    }

    /// <summary>The version a directory such as <c>9.0.3</c> or <c>11.0.0-preview.1</c> is named for, or null.</summary>
    private static InstalledVersion? ReadVersion(string directory)
    {
        var label = Path.GetFileName(directory);
        var release = label.Split('-')[0];
        if (!Version.TryParse(release, out var number))
        {
            return null;
        }

        return new InstalledVersion { Directory = directory, Label = label, Number = number };
    }

    /// <summary>The framework assemblies in <paramref name="directory"/>, by name, told apart from the
    /// native libraries beside them by their file names, as upstream ILSpy does.</summary>
    internal static List<string> ListFrameworkAssemblies(string directory)
    {
        return Directory.EnumerateFiles(directory, "*.dll")
            .Where(IsFrameworkAssemblyFile)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<AssemblyPath> PathsOf(List<string> files)
    {
        return files.ConvertAll(static f => AssemblyPath.Parse(f) ?? throw new InvalidOperationException($"Expected a full path from listing a folder, found {f}."));
    }

    private static bool IsFrameworkAssemblyFile(string path)
    {
        var name = Path.GetFileName(path);
        if (name.EndsWith(NativeHelperSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (name.Contains(NativeLibraryInfix, StringComparison.Ordinal))
        {
            return false;
        }

        if (LowerCaseAssemblies.Contains(name))
        {
            return true;
        }

        return char.IsUpper(name[0]);
    }

    private static List<AssemblyListPreset> FindGacPresets()
    {
        var presets = new List<AssemblyListPreset>();
        if (!OperatingSystem.IsWindows())
        {
            return presets;
        }

        foreach (var (name, fullNames) in GacLists)
        {
            var paths = ResolveInGac(fullNames);
            if (paths.Count == 0)
            {
                continue;
            }

            presets.Add(new AssemblyListPreset { Name = name, Paths = PathsOf(paths) });
        }
        return presets;
    }

    private static List<string> ResolveInGac(string[] fullNames)
    {
        var paths = new List<string>();
        foreach (var fullName in fullNames)
        {
            var reference = AssemblyNameReference.Parse(fullName);
            var path = UniversalAssemblyResolver.GetAssemblyInGac(reference);
            if (path is not null)
            {
                paths.Add(path);
            }
        }
        return paths;
    }

    private const string EcmaKey = "PublicKeyToken=b77a5c561934e089";
    private const string MicrosoftKey = "PublicKeyToken=b03f5f7f11d50a3a";
    private const string WpfKey = "PublicKeyToken=31bf3856ad364e35";

    private static readonly (string Name, string[] FullNames)[] GacLists =
    [
        (".NET Framework 4 (WPF)",
        [
            $"mscorlib, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Core, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Data, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Data.DataSetExtensions, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Xaml, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Xml, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Xml.Linq, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"Microsoft.CSharp, Version=4.0.0.0, Culture=neutral, {MicrosoftKey}",
            $"PresentationCore, Version=4.0.0.0, Culture=neutral, {WpfKey}",
            $"PresentationFramework, Version=4.0.0.0, Culture=neutral, {WpfKey}",
            $"WindowsBase, Version=4.0.0.0, Culture=neutral, {WpfKey}",
        ]),
        (".NET Framework 3.5",
        [
            $"mscorlib, Version=2.0.0.0, Culture=neutral, {EcmaKey}",
            $"System, Version=2.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Core, Version=3.5.0.0, Culture=neutral, {EcmaKey}",
            $"System.Data, Version=2.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Data.DataSetExtensions, Version=3.5.0.0, Culture=neutral, {EcmaKey}",
            $"System.Xml, Version=2.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Xml.Linq, Version=3.5.0.0, Culture=neutral, {EcmaKey}",
            $"PresentationCore, Version=3.0.0.0, Culture=neutral, {WpfKey}",
            $"PresentationFramework, Version=3.0.0.0, Culture=neutral, {WpfKey}",
            $"WindowsBase, Version=3.0.0.0, Culture=neutral, {WpfKey}",
        ]),
        ("ASP.NET MVC 3",
        [
            $"mscorlib, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.ComponentModel.DataAnnotations, Version=4.0.0.0, Culture=neutral, {WpfKey}",
            $"System.Configuration, Version=4.0.0.0, Culture=neutral, {MicrosoftKey}",
            $"System.Core, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Data, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Data.DataSetExtensions, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Data.Entity, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Drawing, Version=4.0.0.0, Culture=neutral, {MicrosoftKey}",
            $"System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, {MicrosoftKey}",
            $"System.Web, Version=4.0.0.0, Culture=neutral, {MicrosoftKey}",
            $"System.Web.Abstractions, Version=4.0.0.0, Culture=neutral, {WpfKey}",
            $"System.Web.ApplicationServices, Version=4.0.0.0, Culture=neutral, {WpfKey}",
            $"System.Web.DynamicData, Version=4.0.0.0, Culture=neutral, {WpfKey}",
            $"System.Web.Entity, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Web.Extensions, Version=4.0.0.0, Culture=neutral, {WpfKey}",
            $"System.Web.Mvc, Version=3.0.0.0, Culture=neutral, {WpfKey}",
            $"System.Web.Routing, Version=4.0.0.0, Culture=neutral, {WpfKey}",
            $"System.Web.Services, Version=4.0.0.0, Culture=neutral, {MicrosoftKey}",
            $"System.Web.WebPages, Version=1.0.0.0, Culture=neutral, {WpfKey}",
            $"System.Web.Helpers, Version=1.0.0.0, Culture=neutral, {WpfKey}",
            $"System.Xml, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"System.Xml.Linq, Version=4.0.0.0, Culture=neutral, {EcmaKey}",
            $"Microsoft.CSharp, Version=4.0.0.0, Culture=neutral, {MicrosoftKey}",
        ]),
    ];

    private sealed record InstalledVersion
    {
        public required string Directory { get; init; }

        public required string Label { get; init; }

        public required Version Number { get; init; }
    }
}

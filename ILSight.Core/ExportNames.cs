using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;

namespace ILSight.Core;

/// <summary>What an export names its projects and solutions, made safe for files and folders. Pure: the export's
/// writing is <see cref="AssemblyExport"/>'s.</summary>
public static class ExportNames
{
    internal const string ProjectExtension = ".csproj";

    /// <summary>Where the project named <paramref name="name"/> of a solution in
    /// <paramref name="folder"/> is written: in a folder of its own, named as it is.</summary>
    public static string ProjectPathIn(string folder, string name)
    {
        var clean = CleanProjectName(name);
        return Path.Combine(folder, clean, clean + ProjectExtension);
    }

    /// <summary>The file name of a solution named <paramref name="name"/>, with or without its
    /// extension, made safe for a file name.</summary>
    public static string SolutionFileName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.EndsWith(SolutionExtension, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - SolutionExtension.Length);
        }

        return WholeProjectDecompiler.CleanUpFileName(trimmed, SolutionExtension);
    }

    private const string SolutionExtension = ".sln";

    /// <summary><paramref name="name"/> made safe for a file and folder name.</summary>
    public static string CleanProjectName(string name)
    {
        return WholeProjectDecompiler.CleanUpDirectoryName(name.Trim());
    }

    /// <summary>A project of each of <paramref name="assemblies"/>, named after it, with a number
    /// after the names that other assemblies already took.</summary>
    public static List<SolutionProject> NameProjects(IReadOnlyList<LoadedModule> assemblies)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var projects = new List<SolutionProject>();
        foreach (var assembly in assemblies)
        {
            var name = ReserveProjectName(taken, assembly.Name);
            projects.Add(new SolutionProject { Assembly = assembly, Name = name });
        }
        return projects;
    }

    private static string ReserveProjectName(HashSet<string> taken, string name)
    {
        var clean = CleanProjectName(name);
        var candidate = clean;
        var number = 2;
        while (!taken.Add(candidate))
        {
            candidate = $"{clean}_{number}";
            number++;
        }
        return candidate;
    }
}

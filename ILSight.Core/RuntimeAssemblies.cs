using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>What <see cref="InstalledRuntime"/> holds. Finding it on the disk is <see cref="RuntimeFolders"/>'.</summary>
public static class RuntimeAssemblies
{
    /// <summary>The runtime's own directory first, then the sibling shared frameworks of the same
    /// version (ASP.NET Core, Windows Desktop) that are installed.</summary>
    public static IReadOnlyList<string> ListDirectories(InstalledRuntime runtime)
    {
        return runtime.DirectoryPaths.Value;
    }

    /// <summary>The runtime's file of the assembly named <paramref name="simpleName"/>, or null.</summary>
    /// <exception cref="IOException">The runtime's directories cannot be listed for now.</exception>
    internal static string? Find(InstalledRuntime runtime, string simpleName)
    {
        return runtime.FilesByName.Value.GetValueOrDefault(simpleName);
    }
}

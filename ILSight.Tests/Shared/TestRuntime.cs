using System.Collections.Immutable;
using ILSight.Core;

namespace ILSight.Tests;

/// <summary>What this machine has installed, read once for all the tests.</summary>
internal static class TestRuntime
{
    private static readonly ImmutableArray<(string Framework, string Path)> Directories = RuntimeFolders.FindDirectories();

    public static readonly InstalledRuntime Installed = new() { Directories = Directories, FilesByName = RuntimeFolders.IndexFiles(Directories) };
}

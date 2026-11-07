using ILSight.Core;

namespace ILSight.App.Tests;

internal static class TestPaths
{
    /// <summary>The absolute path <paramref name="path"/> from the root of the file system, as this OS
    /// writes it, for a file the test only names.</summary>
    public static string Rooted(string path) => Path.GetFullPath("/" + path);

    /// <summary>The path <paramref name="path"/>, which the test names and must be absolute.</summary>
    public static AssemblyPath PathOf(string path) => AssemblyPath.Of(path);

    /// <summary>A path in the temp folder for a file the test never reads, such as one in a saved list.</summary>
    public static AssemblyPath FakePath(string name) => AssemblyPath.Of(Path.Combine(Path.GetTempPath(), name));

    /// <summary>Opens the file at <paramref name="path"/>, which the test names and must be absolute.</summary>
    public static LoadedAssembly Open(this AssemblyList list, string path)
    {
        return AssemblyListing.Open(list, AssemblyPath.Of(path));
    }
}

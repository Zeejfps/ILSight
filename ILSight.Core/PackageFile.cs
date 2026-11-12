namespace ILSight.Core;

/// <summary>A file in a <see cref="AssemblyPackage"/>: its name, its path from the package's root,
/// with <c>/</c> between folders, and its size.</summary>
public sealed record PackageFile
{
    public required string Name { get; init; }

    public required string FullName { get; init; }

    public required long Length { get; init; }

    /// <summary>The path of the folder holding it, from the package's root; empty at the root.</summary>
    public string Folder
    {
        get
        {
            var at = FullName.LastIndexOf('/');
            if (at < 0)
            {
                return "";
            }

            return FullName.Substring(0, at);
        }
    }
}

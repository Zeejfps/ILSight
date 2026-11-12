namespace ILSight.Core;

/// <summary>A file of a package that was not extracted, and why.</summary>
public abstract record ExtractFailure
{
    private ExtractFailure()
    {
    }

    public abstract PackageFile File { get; init; }

    /// <summary>Its path would have put it outside the folder extracted into.</summary>
    public sealed record OutsideFolder(PackageFile File) : ExtractFailure;

    public sealed record NotWritten(PackageFile File, ExportError Error) : ExtractFailure;
}

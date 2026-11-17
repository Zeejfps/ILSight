namespace ILSight.Core;

/// <summary>A file of a package that was not extracted, and why.</summary>
public union ExtractFailure(ExtractFailure.OutsideFolder, ExtractFailure.NotWritten)
{
    /// <summary>Its path would have put it outside the folder extracted into.</summary>
    public sealed record OutsideFolder
    {
        public required PackageFile File { get; init; }
    }

    public sealed record NotWritten
    {
        public required PackageFile File { get; init; }

        public required ExportError Error { get; init; }
    }

    /// <summary>The file that was not extracted.</summary>
    public PackageFile File => this switch
    {
        OutsideFolder outsideFolder => outsideFolder.File,
        NotWritten notWritten => notWritten.File,
    };
}

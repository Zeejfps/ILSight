namespace ILSight.Core;

/// <summary>The paths extracting a package's folder wrote, and the files it could not.</summary>
public sealed record ExtractedFiles
{
    public required IReadOnlyList<string> Written { get; init; }

    public required IReadOnlyList<ExtractFailure> Failures { get; init; }
}

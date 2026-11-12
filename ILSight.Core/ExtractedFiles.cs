namespace ILSight.Core;

/// <summary>The paths extracting a package's folder wrote, and the files it could not.</summary>
public sealed record ExtractedFiles(IReadOnlyList<string> Written, IReadOnlyList<ExtractFailure> Failures);

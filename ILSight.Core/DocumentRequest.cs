namespace ILSight.Core;

/// <summary>What <see cref="NodeDecompiler"/> writes a document of, titled <see cref="Title"/>; a
/// failure to write it is reported as one of <see cref="FullName"/>.</summary>
internal sealed record DocumentRequest
{
    public required string Title { get; init; }

    public required string FullName { get; init; }

    /// <summary>What the document is written of.</summary>
    public required DocumentSubject Subject { get; init; }
}

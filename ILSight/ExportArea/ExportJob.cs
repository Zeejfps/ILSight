namespace ILSight.ExportArea;

/// <summary>An export as the user asked for it: what it is named after, what it writes, and the
/// folder it writes into.</summary>
public sealed record ExportJob
{
    public required string Name { get; init; }

    public required ExportKind Kind { get; init; }

    public required string Folder { get; init; }
}

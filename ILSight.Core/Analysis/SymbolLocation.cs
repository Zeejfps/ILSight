namespace ILSight.Core.Analysis;

/// <summary>Where a symbol is, as read: its declaring type, or for a type its namespace (empty for none), and the
/// name of the assembly it is in (null for an assembly itself). The app arranges them for a person.</summary>
public sealed record SymbolLocation
{
    public required string Container { get; init; }

    public required string? Assembly { get; init; }
}

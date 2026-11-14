namespace ILSight.Core;

/// <summary>What a cref names, as the library resolved it: its short name and where it leads, both null when it
/// resolves to nothing.</summary>
internal sealed record DocCref
{
    public required string? Name { get; init; }
    public required CodeReference? Reference { get; init; }
}

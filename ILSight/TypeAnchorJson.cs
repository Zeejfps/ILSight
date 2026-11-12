namespace ILSight;

/// <summary>A type as a settings file can hold it. Without its assembly or name it is none.</summary>
internal sealed record TypeAnchorJson
{
    public string? AssemblyPath { get; init; }

    public string? FullName { get; init; }
}

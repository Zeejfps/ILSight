using ILSight.Core;

namespace ILSight;

/// <summary>A symbol analyzed, as a settings file can hold it. Without a file it is none.</summary>
internal sealed record CodeAnchorJson
{
    public CodeAnchorKind Kind { get; init; }

    public string? FilePath { get; init; }

    public int Token { get; init; }

    public int ILOffset { get; init; }
}


namespace ILSight.DiagramArea;

/// <summary>What a diagram's card faces are measured for: the cards, which parts of them are open, whether
/// members hidden from other assemblies show, the font and the language.</summary>
public readonly record struct FaceKey
{
    public required int DiagramVersion { get; init; }

    public required int FoldsVersion { get; init; }

    public required bool ShowsHiddenMembers { get; init; }

    public required float FontSize { get; init; }

    public required string? FontFamily { get; init; }

    public required Strings Strings { get; init; }
}

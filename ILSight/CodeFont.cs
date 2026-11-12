namespace ILSight;

/// <summary>An installed font to draw code in: its family's <see cref="Family"/> name and the file
/// of its regular face, kept so the font loads at startup without listing every installed one.</summary>
public sealed record CodeFont
{
    public required string Family { get; init; }

    public required string Path { get; init; }

    public required int FaceIndex { get; init; }
}

namespace ILSight.MetadataArea;

/// <summary>How many lines a text has, and how many characters its longest one.</summary>
internal readonly record struct TextSize
{
    public required int Lines { get; init; }

    public required int Longest { get; init; }
}

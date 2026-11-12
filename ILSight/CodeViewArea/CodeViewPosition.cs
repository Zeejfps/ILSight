namespace ILSight.CodeViewArea;

/// <summary>Where a code view stood: the line at its top, its sideways scroll and its selection —
/// what going back to a place restores.</summary>
public readonly record struct CodeViewPosition
{
    public required int TopLine { get; init; }

    public required float ScrollX { get; init; }

    public required TextPosition Anchor { get; init; }

    public required TextPosition Caret { get; init; }
}

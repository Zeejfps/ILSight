namespace ILSight.Core;

/// <summary>Where a note stands in a document: from <see cref="Column"/> of <see cref="Line"/> to the line's end.</summary>
public readonly record struct PlacedNote
{
    public required int Line { get; init; }

    public required int Column { get; init; }

    public required DocumentNote Note { get; init; }
}

namespace ILSight.Core;

/// <summary>How far an export has got: it works on part <see cref="Part"/> of <see cref="Parts"/>,
/// named <see cref="Name"/>, and has done <see cref="Fraction"/> of the whole export, from 0 to 1,
/// or an unknown share when null.</summary>
public readonly record struct ExportProgress
{
    public required string Name { get; init; }

    public required int Part { get; init; }

    public required int Parts { get; init; }

    public required float? Fraction { get; init; }
}

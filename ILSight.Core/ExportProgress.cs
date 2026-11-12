namespace ILSight.Core;

/// <summary>How far an export has got: it works on part <see cref="Part"/> of <see cref="Parts"/>,
/// named <see cref="Name"/>, and has done <see cref="Fraction"/> of the whole export, from 0 to 1,
/// or an unknown share when null.</summary>
public readonly record struct ExportProgress(string Name, int Part, int Parts, float? Fraction);

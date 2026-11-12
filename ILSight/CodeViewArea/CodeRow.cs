namespace ILSight.CodeViewArea;

/// <summary>A row of a code view, counted from the top of the document as its folds leave it: with a fold
/// collapsed above it, not the number of the line it shows.</summary>
internal readonly record struct CodeRow(int Index);

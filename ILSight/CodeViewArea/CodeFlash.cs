namespace ILSight.CodeViewArea;

/// <summary>The line <see cref="Line"/> flashing since the time <see cref="From"/>, after it was revealed.</summary>
internal readonly record struct CodeFlash(int Line, double From);

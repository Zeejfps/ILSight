namespace ILSight.Core;

/// <summary>A type forwarded to <see cref="Destination"/>, labelled <see cref="Label"/>.</summary>
internal readonly record struct ForwardedLine(string Label, CodeReference? Target, string? Destination);

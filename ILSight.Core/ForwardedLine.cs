namespace ILSight.Core;

/// <summary>A type forwarded to <see cref="Destination"/>, labelled <see cref="Label"/>.</summary>
internal readonly record struct ForwardedLine
{
    public required string Label { get; init; }

    public required CodeReference? Target { get; init; }

    public required string? Destination { get; init; }
}

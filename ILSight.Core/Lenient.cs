namespace ILSight.Core;

/// <summary>What a lenient read made of input whose parts stand alone: the value, and where each part it couldn't read
/// was (such as <c>resources[2]</c>), left out of it, for the reader to say.</summary>
public sealed record Lenient<T>
{
    public required T Value { get; init; }

    public required IReadOnlyList<string> LeftOut { get; init; }
}

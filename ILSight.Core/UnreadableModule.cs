namespace ILSight.Core;

/// <summary>A module whose type system the library could not read, and what it threw.</summary>
public sealed class UnreadableModule
{
    public required LoadedModule Module { get; init; }

    public required Exception Why { get; init; }
}

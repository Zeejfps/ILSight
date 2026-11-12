namespace ILSight.Core.Comparison;

/// <summary>A type or member as one assembly has it: what it points at, and how it is declared, as C# writes it.</summary>
public sealed record ApiSide
{
    public required CodeReference.Entity Reference { get; init; }

    public required string Declaration { get; init; }
}

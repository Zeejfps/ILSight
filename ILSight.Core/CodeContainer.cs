namespace ILSight.Core;

/// <summary>Where a type or member is declared: in a type, by its keyword such as <c>class</c> or
/// <c>record struct</c> and its full name, or in a namespace.</summary>
public sealed record CodeContainer
{
    public required string Keyword { get; init; }

    public required string Name { get; init; }

    public required bool IsNamespace { get; init; }
}

namespace ILSight.Core;

/// <summary>An assembly to export as a project of a solution, under <see cref="Name"/>, which is
/// also the name of the folder the project goes in.</summary>
public sealed record SolutionProject
{
    public required LoadedModule Assembly { get; init; }

    public required string Name { get; init; }
}

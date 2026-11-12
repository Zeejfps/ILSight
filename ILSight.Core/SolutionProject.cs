namespace ILSight.Core;

/// <summary>An assembly to export as a project of a solution, under <see cref="Name"/>, which is
/// also the name of the folder the project goes in.</summary>
public sealed record SolutionProject(LoadedModule Assembly, string Name);

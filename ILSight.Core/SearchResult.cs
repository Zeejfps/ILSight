namespace ILSight.Core;

/// <summary>A search hit: the <see cref="Node"/> found, its label, and where it lives.</summary>
/// <param name="Location">Where it lives, as ILSpy's results show it: the namespace and enclosing
/// type of a type or member, <c>-</c> for the global namespace, the resources file holding a
/// resource entry, an assembly's file; empty for a namespace or a resource of its own.</param>
/// <param name="Fitness">How closely the hit matches, as ILSpy ranks it: the shorter the name the
/// closer, and 0 for compiler-generated names.</param>
public sealed record SearchResult(BrowseNode Node, string Text, string Location, float Fitness);

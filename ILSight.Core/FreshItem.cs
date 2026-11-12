namespace ILSight.Core;

/// <summary>An item of the list read again from its file (<see cref="AssemblyListing.ReadAgain"/>), with what was read
/// with it: for a package, the assemblies read out of it; none for anything else. Listing it in the old item's place
/// (<see cref="ClosableAssemblyList.TryReplace"/>) reads nothing more.</summary>
public sealed record FreshItem
{
    public required LoadedAssembly Item { get; init; }

    /// <summary>The assemblies read out of <see cref="Item"/> when it is a package; empty otherwise.</summary>
    public required IReadOnlyList<LoadedModule> ReadOutOf { get; init; }
}

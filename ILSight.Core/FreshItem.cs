namespace ILSight.Core;

/// <summary>An item of the list read again from its file (<see cref="AssemblyListing.ReadAgain"/>), with what was read
/// with it: for a package, the assemblies read out of it; none for anything else. Listing it in the old item's place
/// (<see cref="ClosableAssemblyList.TryReplace"/>) reads nothing more.</summary>
public sealed record FreshItem
{
    public required LoadedAssembly Item { get; init; }

    /// <summary>The assemblies read out of <see cref="Item"/> when it is a package; empty otherwise.</summary>
    public required IReadOnlyList<LoadedModule> ReadOutOf { get; init; }

    /// <summary>The modules it holds: itself when it is a module, else what was read out of it; for the I/O side to
    /// release when the list doesn't take it (a newer read replaced it, it no longer lists the item, it failed on its
    /// way).</summary>
    public RemovedModules ListModules()
    {
        if (Item is LoadedModule module)
        {
            return new RemovedModules { Modules = [module] };
        }

        return new RemovedModules { Modules = ReadOutOf };
    }
}

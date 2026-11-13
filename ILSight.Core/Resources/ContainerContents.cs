using System.Diagnostics;

namespace ILSight.Core.Resources;

/// <summary>What a resource container holds besides its bytes: a <c>.resources</c> file its entries, read
/// once, and the grid of them; Avalonia's asset container, assets read when it is listed.</summary>
public abstract record ContainerContents
{
    private ContainerContents()
    {
    }

    public sealed record ResourcesFile : ContainerContents
    {
        public required Lazy<List<KeyValuePair<string, object?>>> Entries { get; init; }

        public required Metadata.LazyGrid Grid { get; init; }
    }

    public sealed record AvaloniaAssets : ContainerContents;

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<ResourcesFile, TResult> resourcesFile, Func<AvaloniaAssets, TResult> avaloniaAssets)
    {
        return this switch
        {
            ResourcesFile c => resourcesFile(c),
            AvaloniaAssets c => avaloniaAssets(c),
            _ => throw new UnreachableException($"ContainerContents.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, ResourcesFile, TResult> resourcesFile,
        Func<TState, AvaloniaAssets, TResult> avaloniaAssets)
    {
        return this switch
        {
            ResourcesFile c => resourcesFile(state, c),
            AvaloniaAssets c => avaloniaAssets(state, c),
            _ => throw new UnreachableException($"ContainerContents.Match does not handle {GetType().Name}."),
        };
    }
}

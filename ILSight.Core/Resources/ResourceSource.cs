using System.Diagnostics;
using MetadataResource = ICSharpCode.Decompiler.Metadata.Resource;

namespace ILSight.Core.Resources;

/// <summary>Where a resource's bytes come from: a manifest resource of the module, the bytes of an entry
/// of a container, or the compiled XAML of a <c>.resources</c> file's BAML entry, saved as the XAML.</summary>
public abstract record ResourceSource
{
    private ResourceSource()
    {
    }

    public sealed record Manifest(MetadataResource Resource) : ResourceSource;

    public sealed record Entry(byte[] Data) : ResourceSource;

    public sealed record Baml(byte[] Data) : ResourceSource;

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Manifest, TResult> manifest, Func<Entry, TResult> entry,
        Func<Baml, TResult> baml)
    {
        return this switch
        {
            Manifest c => manifest(c),
            Entry c => entry(c),
            Baml c => baml(c),
            _ => throw new UnreachableException($"ResourceSource.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Manifest, TResult> manifest,
        Func<TState, Entry, TResult> entry, Func<TState, Baml, TResult> baml)
    {
        return this switch
        {
            Manifest c => manifest(state, c),
            Entry c => entry(state, c),
            Baml c => baml(state, c),
            _ => throw new UnreachableException($"ResourceSource.Match does not handle {GetType().Name}."),
        };
    }
}

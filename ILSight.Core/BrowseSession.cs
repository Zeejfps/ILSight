using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace ILSight.Core;

/// <summary>
/// The browse tree of the assemblies it is asked about: the root of each, the children of each node,
/// built once and handed out whole, and the node of each type and member by its handle. A node keeps
/// its identity for as long as its assembly lives, and what the session holds of an assembly goes when
/// the assembly does. All of <see cref="BrowseSessions"/>, which is the way to it, is thread-safe.
/// </summary>
public sealed class BrowseSession
{
    public readonly ConditionalWeakTable<LoadedModule, BrowseNode> ModuleRoots = new();
    public readonly ConditionalWeakTable<LoadedModule, ConcurrentDictionary<string, BrowseNode>> Namespaces = new();
    public readonly ConditionalWeakTable<BrowseNode, IReadOnlyList<BrowseNode>> Nested = new();
    public readonly ConditionalWeakTable<BrowseNode, IReadOnlyList<BrowseNode>> Inner = new();
    public readonly ConditionalWeakTable<LoadedAssembly, BrowseNode> OtherRoots = new();
    public readonly ConditionalWeakTable<LoadedModule, ConcurrentDictionary<EntityHandle, BrowseNode>> Index = new();

    /// <summary>Where each node's children stand, which can only be read or changed holding its lock.</summary>
    internal readonly ConditionalWeakTable<BrowseNode, Guarded<ChildrenState>> Children = new();
}

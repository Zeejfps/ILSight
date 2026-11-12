using System.Collections.Concurrent;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>What building a node's children reads, awaited before the build began
/// (<see cref="BrowseSessions.GetChildrenAsync"/>): the main module of the node's module, as read, for a module, a
/// type reference or a module reference; the type systems a search for derived types can reach; and the modules that
/// search reached that the library could not read.</summary>
internal sealed class ChildrenReads
{
    /// <summary>A build that reads no type system.</summary>
    public static ChildrenReads None => new ChildrenReads();

    public MetadataModule? Main { get; init; }

    public ModuleTypeSystems? Reachable { get; init; }

    public ConcurrentDictionary<LoadedModule, UnreadableModule> Unreadable { get; } = new();

    /// <summary><see cref="Main"/>, which the build of a node reading its module's type system was handed; throws when it
    /// was not, a bug.</summary>
    public MetadataModule RequireMain()
    {
        return Main ?? throw new InvalidOperationException("Children that read a module's type system were built without the read the build awaited.");
    }

    /// <summary><see cref="Reachable"/>, which the build of derived types was handed; throws when it was not, a bug.</summary>
    public ModuleTypeSystems RequireReachable()
    {
        return Reachable ?? throw new InvalidOperationException("Derived types were built without the reads the build awaited.");
    }
}

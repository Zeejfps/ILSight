using ICSharpCode.Decompiler.TypeSystem;
using System.Reflection.Metadata;
using ILSight.Core.Metadata;

namespace ILSight.Core;

/// <summary>The nodes of a <see cref="BrowseSession"/> that paths, references and documentation ids
/// name, found through the files of an <see cref="AssemblyList"/>. May load files and build children:
/// call it off the UI thread.</summary>
public static class NodeLookup
{
    /// <summary>The node <paramref name="path"/> names, opening and listing its assembly if it is not
    /// open and its file still exists, or the root of a file listed as failed. Null if the file cannot
    /// be opened or no longer has that node.</summary>
    public static async Task<BrowseNode?> FindAsync(AssemblyList list, BrowseSession session, NodePath path, CancellationToken ct)
    {
        try
        {
            var item = AssemblyListing.OpenListed(list, path.FilePath);
            if (item is null)
            {
                return null;
            }

            return await BrowseSessions.FindNodeAsync(session, item, path, ct);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>The node <paramref name="path"/> leads to in an assembly the list holds now, or null when
    /// it holds none at that path or the node is no longer there. Opens and lists nothing, so a lookup
    /// that outlives a switch to another list leaves the new list as it is.</summary>
    public static async Task<BrowseNode?> FindListedAsync(AssemblyList list, BrowseSession session, NodePath path, CancellationToken ct)
    {
        var item = AssemblyListing.FindListedLoaded(list, path.FilePath);
        if (item is null)
        {
            return null;
        }

        try
        {
            return await BrowseSessions.FindNodeAsync(session, item, path, ct);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>The node <paramref name="reference"/> points at, opening and listing the assembly that
    /// defines it if it is not open yet and its file can be found. For an entity hidden from the tree,
    /// such as a compiler-generated one, the nearest visible ancestor. Null if the reference cannot be
    /// resolved.</summary>
    public static async Task<BrowseNode?> FindAsync(AssemblyList list, BrowseSession session, CodeReference.Navigable reference, CancellationToken ct)
    {
        try
        {
            var (assembly, handle) = await AssemblyListing.ResolveListedAsync(list, reference, ct);
            if (assembly is null)
            {
                return null;
            }

            if (handle.Kind == HandleKind.AssemblyDefinition)
            {
                return await BrowseSessions.EnsureRootAsync(session, assembly, ct);
            }

            var read = await LoadedAssemblies.ReadTypeSystemAsync(assembly, ct);
            return await read.Match(
                r => BrowseSessions.FindOrAncestorAsync(session, assembly, r.TypeSystem.MainModule, handle, ct),
                static _ => Task.FromResult<BrowseNode?>(null));
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>The metadata table row that defines what <paramref name="reference"/> points at: the row
    /// of a type or member, or an assembly's own row, opening and listing the assembly that defines it
    /// as <see cref="Find(AssemblyList, BrowseSession, CodeReference.Navigable)"/> does. Null if the
    /// reference cannot be resolved.</summary>
    public static async Task<MetadataTarget?> FindMetadataRowAsync(AssemblyList list, BrowseSession session, CodeReference.Navigable reference,
        CancellationToken ct)
    {
        try
        {
            var (assembly, handle) = await AssemblyListing.ResolveListedAsync(list, reference, ct);
            if (assembly is null)
            {
                return null;
            }

            return await BrowseSessions.FindMetadataRowAsync(session, assembly, handle, ct);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>The node of the namespace, type or member <paramref name="idString"/> names, written as
    /// XML documentation ids are, such as <c>T:System.String</c>, in the first of <paramref name="assemblies"/>
    /// that defines it; null when none does.</summary>
    public static async Task<BrowseNode?> FindByIdAsync(BrowseSession session, string idString, IEnumerable<LoadedModule> assemblies, CancellationToken ct)
    {
        foreach (var assembly in assemblies)
        {
            var read = await LoadedAssemblies.ReadTypeSystemAsync(assembly, ct);
            var node = await read.Match(
                r => FindByIdAsync(session, idString, assembly, r.TypeSystem.MainModule, ct),
                static _ => Task.FromResult<BrowseNode?>(null));
            if (node is not null)
            {
                return node;
            }
        }
        return null;
    }

    private static async Task<BrowseNode?> FindByIdAsync(BrowseSession session, string idString, LoadedModule assembly, MetadataModule main,
        CancellationToken ct)
    {
        try
        {
            return await BrowseSessions.FindByIdAsync(session, assembly, main, idString, ct);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}

using ICSharpCode.Decompiler.TypeSystem;
using System.Reflection.Metadata;
using ILSight.Core.Metadata;

namespace ILSight.Core;

/// <summary>The nodes of a <see cref="BrowseSession"/> that paths, references and documentation ids
/// name, found through the files of an <see cref="AssemblyList"/>. May load files and build children:
/// call it off the UI thread. Each answers unreadable for a module whose type system the library could not read where
/// the lookup reads it.</summary>
public static class NodeLookup
{
    /// <summary>The node <paramref name="path"/> names, opening and listing its assembly if it is not
    /// open and its file still exists, or the root of a file listed as failed. Not found if the file cannot be opened
    /// or no longer has that node.</summary>
    public static async Task<Lookup<BrowseNode>> FindAsync(AssemblyList list, BrowseSession session, NodePath path, CancellationToken ct)
    {
        try
        {
            var item = AssemblyListing.OpenListed(list, path.FilePath);
            if (item is null)
            {
                return new Lookup<BrowseNode>.NotFound();
            }

            return await BrowseSessions.FindNodeAsync(session, item, path, ct);
        }
        catch (BadImageFormatException)
        {
            return new Lookup<BrowseNode>.NotFound();
        }
        catch (IOException)
        {
            return new Lookup<BrowseNode>.NotFound();
        }
    }

    /// <summary>The node <paramref name="path"/> leads to in an assembly the list holds now; not found when it
    /// holds none at that path or the node is no longer there. Opens and lists nothing, so a lookup that outlives a
    /// switch to another list leaves the new list as it is.</summary>
    public static async Task<Lookup<BrowseNode>> FindListedAsync(AssemblyList list, BrowseSession session, NodePath path, CancellationToken ct)
    {
        var item = AssemblyListing.FindListedLoaded(list, path.FilePath);
        if (item is null)
        {
            return new Lookup<BrowseNode>.NotFound();
        }

        try
        {
            return await BrowseSessions.FindNodeAsync(session, item, path, ct);
        }
        catch (BadImageFormatException)
        {
            return new Lookup<BrowseNode>.NotFound();
        }
        catch (IOException)
        {
            return new Lookup<BrowseNode>.NotFound();
        }
    }

    /// <summary>The node <paramref name="reference"/> points at, opening and listing the assembly that
    /// defines it if it is not open yet and its file can be found. For an entity hidden from the tree,
    /// such as a compiler-generated one, the nearest visible ancestor. Not found if the reference cannot be
    /// resolved.</summary>
    public static async Task<Lookup<BrowseNode>> FindAsync(AssemblyList list, BrowseSession session, CodeReference.Navigable reference, CancellationToken ct)
    {
        try
        {
            using var reading = LoadedAssemblies.BeginReading();
            var resolved = await AssemblyListing.ResolveListedAsync(list, reading, reference, ct);
            return await resolved.ThenFindAsync((target, c) => FindTargetAsync(session, reading, target.Assembly, target.Handle, c), ct);
        }
        catch (BadImageFormatException)
        {
            return new Lookup<BrowseNode>.NotFound();
        }
        catch (IOException)
        {
            return new Lookup<BrowseNode>.NotFound();
        }
    }

    /// <summary>The node of what <paramref name="handle"/> defines in <paramref name="assembly"/>, or of the assembly
    /// itself, once the read of its type system ended, under <paramref name="reading"/>, the unit's; not found when the
    /// assembly was released (closed) meanwhile.</summary>
    private static async Task<Lookup<BrowseNode>> FindTargetAsync(BrowseSession session, ModuleReading reading, LoadedModule assembly, EntityHandle handle,
        CancellationToken ct)
    {
        if (handle.Kind == HandleKind.AssemblyDefinition)
        {
            return new Lookup<BrowseNode>.Found { Value = await BrowseSessions.EnsureRootAsync(session, assembly, ct) };
        }

        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, assembly, ct);
        return await read.Match(
            async r => Lookup.FromNullable(await BrowseSessions.FindOrAncestorAsync(session, assembly, r.TypeSystem.MainModule, handle, ct)),
            u => Task.FromResult(Lookup.FromUnreadable<BrowseNode>(assembly, u)),
            static _ => Task.FromResult<Lookup<BrowseNode>>(new Lookup<BrowseNode>.NotFound()));
    }

    /// <summary>The metadata table row that defines what <paramref name="reference"/> points at: the row
    /// of a type or member, or an assembly's own row, opening and listing the assembly that defines it
    /// as <see cref="FindAsync(AssemblyList, BrowseSession, CodeReference.Navigable, CancellationToken)"/> does. Not
    /// found if the reference cannot be resolved.</summary>
    public static async Task<Lookup<MetadataTarget>> FindMetadataRowAsync(AssemblyList list, BrowseSession session, CodeReference.Navigable reference,
        CancellationToken ct)
    {
        try
        {
            using var reading = LoadedAssemblies.BeginReading();
            var resolved = await AssemblyListing.ResolveListedAsync(list, reading, reference, ct);
            return await resolved.ThenFindAsync((target, c) => BrowseSessions.FindMetadataRowAsync(session, reading, target.Assembly, target.Handle, c), ct);
        }
        catch (BadImageFormatException)
        {
            return new Lookup<MetadataTarget>.NotFound();
        }
        catch (IOException)
        {
            return new Lookup<MetadataTarget>.NotFound();
        }
    }

    /// <summary>The node of the namespace, type or member <paramref name="idString"/> names, written as
    /// XML documentation ids are, such as <c>T:System.String</c>, in the first of <paramref name="assemblies"/>
    /// that defines it; not found when none does. When none of those read defines it and the library could not read
    /// one of them, that one, the first, which may define it: unreadable. One released (closed) meanwhile is passed
    /// over.</summary>
    public static async Task<Lookup<BrowseNode>> FindByIdAsync(BrowseSession session, string idString, IEnumerable<LoadedModule> assemblies,
        CancellationToken ct)
    {
        Lookup<BrowseNode> answer = new Lookup<BrowseNode>.NotFound();
        using var reading = LoadedAssemblies.BeginReading();
        foreach (var assembly in assemblies)
        {
            var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, assembly, ct);
            var found = await read.Match(
                r => FindByIdAsync(session, idString, assembly, r.TypeSystem.MainModule, ct),
                u => Task.FromResult(Lookup.FromUnreadable<BrowseNode>(assembly, u)),
                static _ => Task.FromResult<Lookup<BrowseNode>>(new Lookup<BrowseNode>.NotFound()));
            if (found is Lookup<BrowseNode>.Found)
            {
                return found;
            }

            if (answer is Lookup<BrowseNode>.NotFound)
            {
                answer = found;
            }
        }
        return answer;
    }

    private static async Task<Lookup<BrowseNode>> FindByIdAsync(BrowseSession session, string idString, LoadedModule assembly, MetadataModule main,
        CancellationToken ct)
    {
        try
        {
            return Lookup.FromNullable(await BrowseSessions.FindByIdAsync(session, assembly, main, idString, ct));
        }
        catch (BadImageFormatException)
        {
            return new Lookup<BrowseNode>.NotFound();
        }
        catch (IOException)
        {
            return new Lookup<BrowseNode>.NotFound();
        }
    }
}

using ICSharpCode.Decompiler.TypeSystem;
using System.Reflection.Metadata;
using ILSight.Core.Metadata;

namespace ILSight.Core;

/// <summary>The nodes of a <see cref="BrowseSession"/> that paths, references and documentation ids
/// name, found through the files of an <see cref="AssemblyList"/>. May load files and build children:
/// call it off the UI thread. Each walks one level at a time: each level's read of the library under its own catch
/// (a file's load, a listing's children, a module's type system, the metadata a step reads), the node made and the next
/// step chosen after it, on what was read. Each answers unreadable for a module the library failed reading on the way,
/// the user's data's failure, said as the module's; a file on the way that can't be read as the lookup's
/// <see cref="Outcome{T}.Failed"/> with its <see cref="AssemblyLoadException"/>, and children on the way that failed to
/// build as that failure. No catch holds the app's own work on the way: what it throws goes on to the edge, a bug. The
/// cancellation of the token it was given is let through.</summary>
public static class NodeLookup
{
    /// <summary>The node <paramref name="path"/> names, opening and listing its assembly if it is not
    /// open and its file still exists, or the root of a file listed as failed. Not found if the file no longer has that
    /// node; the failure, with why, of a file there that can't be opened, or of children on the way that failed to
    /// build, never not found for what they could hold.</summary>
    public static Task<Outcome<Lookup<BrowseNode>>> FindAsync(AssemblyList list, BrowseSession session, NodePath path, CancellationToken ct)
    {
        return FindOpeningAsync(list, session, path, static failed => failed, ct);
    }

    /// <summary>The node <paramref name="path"/> names, to show expanded again, as
    /// <see cref="FindAsync(AssemblyList, BrowseSession, NodePath, CancellationToken)"/> finds it, except not found
    /// when children on the way failed to build: the tree says that failure where it lists them, and nothing under
    /// them can be shown expanded.</summary>
    public static Task<Outcome<Lookup<BrowseNode>>> FindToExpandAsync(AssemblyList list, BrowseSession session, NodePath path, CancellationToken ct)
    {
        return FindOpeningAsync(list, session, path, static _ => new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound()), ct);
    }

    // placeFailed answers for children on the way that failed to build; a file that can't be opened is the lookup's
    // failure.
    private static async Task<Outcome<Lookup<BrowseNode>>> FindOpeningAsync(AssemblyList list, BrowseSession session, NodePath path,
        Func<Outcome<Lookup<BrowseNode>>.Failed, Outcome<Lookup<BrowseNode>>> placeFailed, CancellationToken ct)
    {
        LoadedAssembly? item;
        try
        {
            item = AssemblyListing.OpenListed(list, path.FilePath);
        }
        catch (AssemblyLoadException e)
        {
            return new Outcome<Lookup<BrowseNode>>.Failed(e);
        }

        if (item is null)
        {
            return new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound());
        }

        var found = await BrowseSessions.FindNodeAsync(session, item, path, ct);
        return found is Outcome<Lookup<BrowseNode>>.Failed failed ? placeFailed(failed) : found;
    }

    /// <summary>The node <paramref name="path"/> leads to in an assembly the list holds now; not found when it
    /// holds none at that path or the node is no longer there; the failure, with why, of children on the way that
    /// failed to build, never not found for what they could hold. Opens and lists nothing, so a lookup that outlives a
    /// switch to another list leaves the new list as it is.</summary>
    public static async Task<Outcome<Lookup<BrowseNode>>> FindListedAsync(AssemblyList list, BrowseSession session, NodePath path, CancellationToken ct)
    {
        var item = AssemblyListing.FindListedLoaded(list, path.FilePath);
        if (item is null)
        {
            return new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound());
        }

        return await BrowseSessions.FindNodeAsync(session, item, path, ct);
    }

    /// <summary>The node <paramref name="reference"/> points at, opening and listing the assembly that
    /// defines it if it is not open yet and its file can be found. For an entity hidden from the tree,
    /// such as a compiler-generated one, the nearest visible ancestor. Not found if the reference cannot be
    /// resolved; the failure, with why, of a file resolving it needs that can't be read, or of the children that would
    /// list it, never its ancestor nor not found.</summary>
    public static async Task<Outcome<Lookup<BrowseNode>>> FindAsync(AssemblyList list, BrowseSession session, CodeReference.Navigable reference,
        CancellationToken ct)
    {
        using var reading = LoadedAssemblies.BeginReading();
        var resolved = await ResolveAsync(list, reading, reference, ct);
        return await resolved.Match(
            ok => ok.Value.ThenTryFindAsync((target, c) => FindTargetAsync(session, reading, target.Assembly, target.Handle, c), ct),
            static failed => Task.FromResult<Outcome<Lookup<BrowseNode>>>(new Outcome<Lookup<BrowseNode>>.Failed(failed.Error)));
    }

    // The listed assembly defining what reference points at, and its handle there; a file resolving it needs that
    // can't be read, the lookup's failure. Each library read on the way is under its own catch (ResolveListedAsync).
    private static async Task<Outcome<Lookup<(LoadedModule Assembly, EntityHandle Handle)>>> ResolveAsync(AssemblyList list, ModuleReading reading,
        CodeReference.Navigable reference, CancellationToken ct)
    {
        try
        {
            return new Outcome<Lookup<(LoadedModule Assembly, EntityHandle Handle)>>.Ok(await AssemblyListing.ResolveListedAsync(list, reading, reference, ct));
        }
        catch (AssemblyLoadException e)
        {
            return new Outcome<Lookup<(LoadedModule Assembly, EntityHandle Handle)>>.Failed(e);
        }
        catch (DefinitionUnreadException e)
        {
            return new Outcome<Lookup<(LoadedModule Assembly, EntityHandle Handle)>>.Failed(e);
        }
    }

    private static async Task<Outcome<Lookup<BrowseNode>>> FindTargetAsync(BrowseSession session, ModuleReading reading, LoadedModule assembly,
        EntityHandle handle, CancellationToken ct)
    {
        if (handle.Kind == HandleKind.AssemblyDefinition)
        {
            return new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Found { Value = await BrowseSessions.EnsureRootAsync(session, assembly, ct) });
        }

        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, assembly, ct);
        return await read.Match(
            r => BrowseSessions.FindOrAncestorAsync(session, assembly, r.TypeSystem.MainModule, handle, ct),
            u => Task.FromResult<Outcome<Lookup<BrowseNode>>>(new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromUnreadable<BrowseNode>(assembly, u))),
            static _ => Task.FromResult<Outcome<Lookup<BrowseNode>>>(new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound())));
    }

    /// <summary>The metadata table row that defines what <paramref name="reference"/> points at: the row
    /// of a type or member, or an assembly's own row, opening and listing the assembly that defines it
    /// as <see cref="FindAsync(AssemblyList, BrowseSession, CodeReference.Navigable, CancellationToken)"/> does. Not
    /// found if the reference cannot be resolved; the failure, with why, of a file resolving it needs that can't be
    /// read, or of the folders or tables that would list the row.</summary>
    public static async Task<Outcome<Lookup<MetadataTarget>>> FindMetadataRowAsync(AssemblyList list, BrowseSession session,
        CodeReference.Navigable reference, CancellationToken ct)
    {
        using var reading = LoadedAssemblies.BeginReading();
        var resolved = await ResolveAsync(list, reading, reference, ct);
        return await resolved.Match(
            ok => ok.Value.ThenTryFindAsync((target, c) => BrowseSessions.FindMetadataRowAsync(session, reading, target.Assembly, target.Handle, c), ct),
            static failed => Task.FromResult<Outcome<Lookup<MetadataTarget>>>(new Outcome<Lookup<MetadataTarget>>.Failed(failed.Error)));
    }

    /// <summary>The node of the namespace, type or member <paramref name="idString"/> names, written as
    /// XML documentation ids are, such as <c>T:System.String</c>, in the first of <paramref name="assemblies"/>
    /// that defines it; not found when none does. When none of those read defines it and the library could not read
    /// one of them, that one, the first, which may define it: unreadable, also when the library failed looking in it,
    /// or <see cref="Outcome{T}.Failed"/> when the children that would list it failed to build. One released (closed)
    /// meanwhile is passed over.</summary>
    public static async Task<Outcome<Lookup<BrowseNode>>> FindByIdAsync(BrowseSession session, string idString, IEnumerable<LoadedModule> assemblies,
        CancellationToken ct)
    {
        Outcome<Lookup<BrowseNode>> answer = new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound());
        using var reading = LoadedAssemblies.BeginReading();
        foreach (var assembly in assemblies)
        {
            var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, assembly, ct);
            var found = await read.Match(
                r => BrowseSessions.FindByIdAsync(session, assembly, r.TypeSystem.MainModule, idString, ct),
                u => Task.FromResult<Outcome<Lookup<BrowseNode>>>(new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromUnreadable<BrowseNode>(assembly, u))),
                static _ => Task.FromResult<Outcome<Lookup<BrowseNode>>>(new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound())));
            if (found is Outcome<Lookup<BrowseNode>>.Ok { Value: Lookup<BrowseNode>.Found })
            {
                return found;
            }

            if (answer is Outcome<Lookup<BrowseNode>>.Ok { Value: Lookup<BrowseNode>.NotFound })
            {
                answer = found;
            }
        }
        return answer;
    }
}

using ICSharpCode.Decompiler.TypeSystem;
using System.Reflection.Metadata;
using ILSight.Core.Metadata;

namespace ILSight.Core;

/// <summary>The nodes of a <see cref="BrowseSession"/> that paths, references and documentation ids
/// name, found through the files of an <see cref="AssemblyList"/>. May load files and build children:
/// call it off the UI thread. Each answers unreadable for a module whose type system the library could not read where
/// the lookup reads it, and what the library throws reading the modules as the lookup's
/// <see cref="Outcome{T}.Failed"/>; the cancellation of the token it was given is let through.</summary>
public static class NodeLookup
{
    /// <summary>The node <paramref name="path"/> names, opening and listing its assembly if it is not
    /// open and its file still exists, or the root of a file listed as failed. Not found if the file cannot be opened
    /// or no longer has that node; the failure, with why, of children on the way that failed to build, never not found
    /// for what they could hold.</summary>
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

    // placeFailed answers for children on the way that failed to build; what the library throws is the lookup's failure.
    private static async Task<Outcome<Lookup<BrowseNode>>> FindOpeningAsync(AssemblyList list, BrowseSession session, NodePath path,
        Func<Outcome<Lookup<BrowseNode>>.Failed, Outcome<Lookup<BrowseNode>>> placeFailed, CancellationToken ct)
    {
        Outcome<Lookup<BrowseNode>> found;
        try
        {
            var item = AssemblyListing.OpenListed(list, path.FilePath);
            found = item is null
                ? new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound())
                : await BrowseSessions.FindNodeAsync(session, item, path, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<Lookup<BrowseNode>>.Failed(e);
        }

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

        Outcome<Lookup<BrowseNode>> found;
        try
        {
            found = await BrowseSessions.FindNodeAsync(session, item, path, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<Lookup<BrowseNode>>.Failed(e);
        }

        return found;
    }

    /// <summary>The node <paramref name="reference"/> points at, opening and listing the assembly that
    /// defines it if it is not open yet and its file can be found. For an entity hidden from the tree,
    /// such as a compiler-generated one, the nearest visible ancestor. Not found if the reference cannot be
    /// resolved; the failure, with why, of the children that would list it, never its ancestor nor not found.</summary>
    public static async Task<Outcome<Lookup<BrowseNode>>> FindAsync(AssemblyList list, BrowseSession session, CodeReference.Navigable reference,
        CancellationToken ct)
    {
        using var reading = LoadedAssemblies.BeginReading();
        Outcome<Lookup<BrowseNode>> found;
        try
        {
            var resolved = await AssemblyListing.ResolveListedAsync(list, reading, reference, ct);
            found = await resolved.ThenTryFindAsync((target, c) => FindTargetAsync(session, reading, target.Assembly, target.Handle, c), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<Lookup<BrowseNode>>.Failed(e);
        }

        return found;
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
    /// found if the reference cannot be resolved; the failure, with why, of the folders or tables that would list the
    /// row.</summary>
    public static async Task<Outcome<Lookup<MetadataTarget>>> FindMetadataRowAsync(AssemblyList list, BrowseSession session,
        CodeReference.Navigable reference, CancellationToken ct)
    {
        using var reading = LoadedAssemblies.BeginReading();
        Outcome<Lookup<MetadataTarget>> found;
        try
        {
            var resolved = await AssemblyListing.ResolveListedAsync(list, reading, reference, ct);
            found = await resolved.ThenTryFindAsync((target, c) => BrowseSessions.FindMetadataRowAsync(session, reading, target.Assembly, target.Handle, c), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<Lookup<MetadataTarget>>.Failed(e);
        }

        return found;
    }

    /// <summary>The node of the namespace, type or member <paramref name="idString"/> names, written as
    /// XML documentation ids are, such as <c>T:System.String</c>, in the first of <paramref name="assemblies"/>
    /// that defines it; not found when none does. When none of those read defines it and the library could not read
    /// one of them, that one, the first, which may define it: unreadable, or <see cref="Outcome{T}.Failed"/> when the
    /// library failed looking in it, or the children that would list it failed to build. One released (closed)
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
                r => FindByIdAsync(session, idString, assembly, r.TypeSystem.MainModule, ct),
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

    private static async Task<Outcome<Lookup<BrowseNode>>> FindByIdAsync(BrowseSession session, string idString, LoadedModule assembly,
        MetadataModule main, CancellationToken ct)
    {
        try
        {
            return await BrowseSessions.FindByIdAsync(session, assembly, main, idString, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<Lookup<BrowseNode>>.Failed(e);
        }
    }
}

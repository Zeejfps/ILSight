using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Output;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>
/// How the real ILSpy names the nodes of its assembly tree, so bookmark files can be exchanged with
/// it: the assembly by its file, a namespace by its name, a type by its reflection name, and a
/// member by its kind and IL signature. Each name is what ILSpy's tree node returns from ToString.
/// </summary>
public static class ILSpyTree
{
    private static readonly ILAmbience Signatures = new()
    {
        ConversionFlags = ConversionFlags.ShowTypeParameterList
            | ConversionFlags.PlaceReturnTypeAfterParameterList
            | ConversionFlags.ShowReturnType
            | ConversionFlags.ShowParameterList
            | ConversionFlags.ShowParameterModifiers,
    };

    /// <summary>The name ILSpy's tree gives the node standing for <paramref name="node"/>, or null
    /// for a node it has no named counterpart of: the one kept when the node was made.</summary>
    public static string? NameOf(BrowseNode node)
    {
        return node.ILSpyName;
    }

    /// <summary>The name ILSpy's tree gives a node of <paramref name="subject"/>, read from what it stands for, as
    /// <see cref="BrowseNodes"/> makes the node.</summary>
    internal static string? NameOf(NodeSubject subject)
    {
        return subject.Match<string?>(
            module: static module => module.Value.FilePath.Text,
            @namespace: static ns => ns.Value.Name,
            failedAssembly: static _ => null,
            typeDefinition: static type => NodeDefinitions.GetSymbol(type.Definition).ReflectionName,
            memberDefinition: static member => NodeDefinitions.GetSymbol(member.Definition) switch
            {
                IMethod method => "Method " + Signatures.ConvertSymbol(method),
                IProperty property => "Property " + Signatures.ConvertSymbol(property),
                IField field => "Field " + field.Name,
                IEvent @event => "Event " + @event.Name,
                _ => null,
            },
            references: static _ => null,
            assemblyReference: static _ => null,
            referencedTypes: static _ => null,
            typeReference: static _ => null,
            memberReference: static _ => null,
            moduleReference: static _ => null,
            importedMethod: static _ => null,
            forwardedType: static _ => null,
            baseTypes: static _ => null,
            baseType: static _ => null,
            derivedTypes: static _ => null,
            derivedType: static _ => null,
            unreadableModule: static _ => null,
            resources: static _ => null,
            resourceContainer: static _ => null,
            resourceEntry: static _ => null,
            metadata: static _ => null,
            metadataTables: static _ => null,
            metadataTable: static _ => null,
            package: static _ => null,
            packageFile: static _ => null);
    }

    /// <summary>The names of the nodes down from the assembly to <paramref name="node"/>, as ILSpy's
    /// tree finds a node by; null when one of them has no name there.</summary>
    public static IReadOnlyList<string>? PathOf(BrowseNode node)
    {
        var path = new List<string>();
        for (var n = node; n is not null; n = n.Parent)
        {
            var name = NameOf(n);
            if (name is null)
            {
                return null;
            }

            path.Add(name);
        }
        path.Reverse();
        return path;
    }

    /// <summary>The node a bookmark ILSpy made in the assembly at <paramref name="filePath"/> sits
    /// in, as the bookmarks describe it for ILSpy (<see cref="ILSpyBookmarkInfo"/>): the node <paramref name="treePath"/> names, else the
    /// node of the entity with metadata <paramref name="token"/>, when its full name is
    /// <paramref name="memberName"/>, or its nearest ancestor. Loads the assembly without listing it; not found
    /// when its file is gone or has neither (a token past the end of its table names nothing); unreadable when the
    /// library could not read the module either is looked for in, or failed resolving or naming the entity the token
    /// names, and the other found nothing; a file there that can't be read, and children on the way that failed to
    /// build (never not found, nor an ancestor, for what they could hold), <see cref="Outcome{T}.Failed"/>. Each read
    /// of the library on the way is under its own catch, the next step chosen after it, so a bug of the app's on the
    /// way goes on to the edge. The cancellation of <paramref name="ct"/> is let through.</summary>
    public static async Task<Outcome<Lookup<BrowseNode>>> LocateAsync(AssemblyList list, BrowseSession session, AssemblyPath filePath,
        IReadOnlyList<string>? treePath, uint token, string memberName, CancellationToken ct)
    {
        var byPath = NotFound;
        if (treePath is not null && treePath.Count > 0)
        {
            byPath = await FindAsync(list, session, treePath, ct);
        }

        var byToken = byPath is Outcome<Lookup<BrowseNode>>.Ok { Value: Lookup<BrowseNode>.Found }
            ? byPath
            : await FindEntityAsync(list, session, filePath, token, memberName, ct);

        // What the token finds wins; when it finds nothing, what the path came to.
        return byToken is Outcome<Lookup<BrowseNode>>.Ok { Value: Lookup<BrowseNode>.NotFound } ? byPath : byToken;
    }

    private static Outcome<Lookup<BrowseNode>> NotFound => new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound());

    private static async Task<Outcome<Lookup<BrowseNode>>> FindAsync(AssemblyList list, BrowseSession session, IReadOnlyList<string> path,
        CancellationToken ct)
    {
        var filePath = AssemblyPath.Parse(path[0]);
        if (filePath is null)
        {
            return NotFound;
        }

        var loaded = FindModule(list, filePath);
        if (loaded is Outcome<Lookup<LoadedModule>>.Failed failed)
        {
            return new Outcome<Lookup<BrowseNode>>.Failed(failed.Error);
        }

        if (loaded is not Outcome<Lookup<LoadedModule>>.Ok { Value: Lookup<LoadedModule>.Found assembly })
        {
            return NotFound;
        }

        var root = await BrowseSessions.EnsureRootAsync(session, assembly.Value, ct);
        Outcome<Lookup<BrowseNode>> reached = new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Found { Value = root });
        for (var i = 1; i < path.Count; i++)
        {
            if (reached is not Outcome<Lookup<BrowseNode>>.Ok { Value: Lookup<BrowseNode>.Found node })
            {
                return reached;
            }

            var children = await BrowseSessions.ReadChildrenAsync(session, node.Value, ct);
            reached = BrowseSessions.FindAmongReadable(children, path[i], static (listed, name) => listed.FirstOrDefault(child => NameOf(child) == name));
        }
        return reached;
    }

    private static async Task<Outcome<Lookup<BrowseNode>>> FindEntityAsync(AssemblyList list, BrowseSession session, AssemblyPath filePath,
        uint token, string memberName, CancellationToken ct)
    {
        if (token == 0)
        {
            return NotFound;
        }

        var loaded = FindModule(list, filePath);
        if (loaded is Outcome<Lookup<LoadedModule>>.Failed failed)
        {
            return new Outcome<Lookup<BrowseNode>>.Failed(failed.Error);
        }

        if (loaded is not Outcome<Lookup<LoadedModule>>.Ok { Value: Lookup<LoadedModule>.Found found })
        {
            return NotFound;
        }

        var assembly = found.Value;
        using var reading = LoadedAssemblies.BeginReading();
        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, assembly, ct);
        return await read.Match(
            r => FindEntityAsync(session, assembly, r.TypeSystem.MainModule, token, memberName, ct),
            u => Task.FromResult<Outcome<Lookup<BrowseNode>>>(new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromUnreadable<BrowseNode>(assembly, u))),
            static _ => Task.FromResult(NotFound));
    }

    private static async Task<Outcome<Lookup<BrowseNode>>> FindEntityAsync(BrowseSession session, LoadedModule assembly, MetadataModule main,
        uint token, string memberName, CancellationToken ct)
    {
        var resolved = ResolveEntity(assembly, main, (int)token);
        if (resolved is Lookup<IEntity>.Unreadable unreadable)
        {
            return new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Unreadable { Module = unreadable.Module });
        }

        if (resolved is not Lookup<IEntity>.Found found)
        {
            return NotFound;
        }

        var read = ReadSavedEntity(assembly, found.Value);
        if (read is Lookup<SavedEntity>.Unreadable unreadableName)
        {
            return new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Unreadable { Module = unreadableName.Module });
        }

        var saved = (Lookup<SavedEntity>.Found)read;

        // After a rebuild the token can name another entity: the app's check, on the name read.
        if (saved.Value.FullName != memberName)
        {
            return NotFound;
        }

        return await BrowseSessions.FindOrAncestorAsync(session, assembly, main, saved.Value.Handle, ct);
    }

    // What the bookmark's token names, read out of the module: its full name and handle.
    private sealed record SavedEntity
    {
        public required string FullName { get; init; }

        public required EntityHandle Handle { get; init; }
    }

    // The library's resolving of token, under its own catch: none when it names no row of the module or the row
    // resolves to nothing; the module unreadable, with why, when the library fails reading it, whatever it throws (the
    // decompiler's every failure on the data is the data's, as ILSight states).
    private static Lookup<IEntity> ResolveEntity(LoadedModule assembly, MetadataModule main, int token)
    {
        try
        {
            if (!SavedTokens.IsInModule(main, token))
            {
                return new Lookup<IEntity>.NotFound();
            }

            var entity = main.ResolveEntity(MetadataTokens.EntityHandle(token));
            return Lookup.FromNullable(entity);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Lookup<IEntity>.Unreadable { Module = new UnreadableModule { Module = assembly, Why = e } };
        }
    }

    // The library's read of the resolved entity's full name and handle, under its own catch: what it throws (a name it
    // decodes on first use), the module unreadable, with why.
    private static Lookup<SavedEntity> ReadSavedEntity(LoadedModule assembly, IEntity entity)
    {
        try
        {
            return new Lookup<SavedEntity>.Found { Value = new SavedEntity { FullName = entity.FullName, Handle = entity.MetadataToken } };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Lookup<SavedEntity>.Unreadable { Module = new UnreadableModule { Module = assembly, Why = e } };
        }
    }

    // The file at filePath loaded without listing it, under its own catch: the module; not found when no file is there or it is a
    // package; a file there that can't be read, the lookup's failure with why.
    private static Outcome<Lookup<LoadedModule>> FindModule(AssemblyList list, AssemblyPath filePath)
    {
        try
        {
            var module = AssemblyListing.FindAssemblyToLookIn(list, filePath);
            return new Outcome<Lookup<LoadedModule>>.Ok(Lookup.FromNullable(module));
        }
        catch (AssemblyLoadException e)
        {
            return new Outcome<Lookup<LoadedModule>>.Failed(e);
        }
    }
}

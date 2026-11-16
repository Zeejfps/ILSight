using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core;

/// <summary>A module's resolver as the library asks it within one unit: the module a reference names is handed out only
/// once it is added to the unit's reading, so it isn't released while the unit reads it; one closed is handed out as
/// not found. A type system's build also records what it was handed (<see cref="TypeSystemBuild{T}"/>), as values: it
/// holds nothing past the unit. A reference whose file is there but can't be read is read by name only, the failure
/// noted on the module (<see cref="ModuleLibrary.NoteUnreadReference"/>); one not found is read by name only and noted
/// as not found (<see cref="ModuleLibrary.NoteMissingReference"/>). The app's code the library calls back while it
/// reads: its work (the app's lookup of the file, handing it out, noting what it couldn't) marks
/// <see cref="Callbacks"/> when it throws, so the read's catch lets it through (CD4); the reference row it is handed
/// and the library's own resolver it falls back on are the read's, outside that count (CF9).</summary>
internal sealed class ReadingResolver : IAssemblyResolver
{
    private readonly ModuleLibrary _resolving;
    private readonly ModuleReadLease _reading;
    private readonly List<ModuleLibrary> _handedOut = [];

    public ReadingResolver(ModuleLibrary resolving, ModuleReadLease reading)
    {
        _resolving = resolving;
        _reading = reading;
    }

    /// <summary>What the resolver's work marks when it throws: the read's, which the catch around it reads.</summary>
    public CallbackFailure Callbacks { get; init; } = new();

    /// <summary>The modules handed out so far, each once.</summary>
    public IReadOnlyList<ModuleLibrary> ListHandedOut()
    {
        lock (_handedOut)
        {
            return [.. _handedOut];
        }
    }

    // What the library hands in and what its own resolver searches are the read's, outside the count (CF9): the
    // reference row read whole first (its properties read the user's metadata each time they are asked), then the
    // app's search, the library's where the app's finds nothing, and the app's again on what that answered.
    public MetadataFile? Resolve(IAssemblyReference reference)
    {
        var facts = AssemblyReferenceFacts.Of(reference);
        var begun = Callbacks.Run((Resolver: this, facts), static s => s.Resolver.BeginResolve(s.facts));
        if (begun.Done)
        {
            return begun.File;
        }

        var probed = _resolving.Resolver.ProbeLibrary(facts);
        return Callbacks.Run((Resolver: this, facts, probed), static s => s.Resolver.EndResolve(s.facts, s.probed));
    }

    private (bool Done, MetadataFile? File) BeginResolve(AssemblyReferenceFacts reference)
    {
        (bool Done, LoadedModule? Found) begun;
        try
        {
            begun = _resolving.Resolver.BeginResolveAssembly(reference);
        }
        catch (AssemblyLoadException e)
        {
            return (true, ReadByNameOnly(reference.Name, e));
        }

        return begun.Done ? (true, HandOut(begun.Found, reference.Name)) : (false, null);
    }

    private MetadataFile? EndResolve(AssemblyReferenceFacts reference, LibraryProbe probed)
    {
        LoadedModule? found;
        try
        {
            found = _resolving.Resolver.EndResolveAssembly(reference, probed);
        }
        catch (AssemblyLoadException e)
        {
            return ReadByNameOnly(reference.Name, e);
        }

        return HandOut(found, reference.Name);
    }

    // The main module's file name is read of the library's object before the count (CF9, CJ9); the app's search for
    // the module beside it is the counted work.
    public MetadataFile? ResolveModule(MetadataFile mainModule, string moduleName)
    {
        var mainModuleFileName = mainModule.FileName;
        return Callbacks.Run((Resolver: this, mainModuleFileName, moduleName), static s => s.Resolver.FindModule(s.mainModuleFileName, s.moduleName));
    }

    private MetadataFile? FindModule(string mainModuleFileName, string moduleName)
    {
        LoadedModule? found;
        try
        {
            found = _resolving.Resolver.FindModule(mainModuleFileName, moduleName);
        }
        catch (AssemblyLoadException e)
        {
            return ReadByNameOnly(moduleName, e);
        }

        return HandOut(found, moduleName);
    }

    // A file found for the reference that can't be read degrades the type system rather than failing it: the reference
    // is read by name only, as one not found, and the failure is noted as the reference's, for the code to say and the
    // user to be told once. A lookup whose answer is in that file fails with it (AssemblyListing).
    private MetadataFile? ReadByNameOnly(string name, AssemblyLoadException error)
    {
        _resolving.NoteUnreadReference(name, error);
        return null;
    }

    public IDisposable? BeginSnapshot()
    {
        return null;
    }

    public Task<MetadataFile?> ResolveAsync(IAssemblyReference reference)
    {
        return Task.FromResult(Resolve(reference));
    }

    public Task<MetadataFile?> ResolveModuleAsync(MetadataFile mainModule, string moduleName)
    {
        return Task.FromResult(ResolveModule(mainModule, moduleName));
    }

    // A reference not found is read by name only too: the code says it was read without it, but it is no failure.
    private MetadataFile? HandOut(LoadedModule? module, string name)
    {
        if (module is null)
        {
            _resolving.NoteMissingReference(name);
            return null;
        }

        var library = LoadedAssemblies.GetKept(module);
        // The resolving module's own file needs no reading of its own: the unit holds it.
        if (ReferenceEquals(library, _resolving))
        {
            return library.File;
        }

        if (!_reading.TryAdd(library))
        {
            return null;
        }

        Record(library);
        return library.File;
    }

    private void Record(ModuleLibrary library)
    {
        lock (_handedOut)
        {
            if (!_handedOut.Contains(library))
            {
                _handedOut.Add(library);
            }
        }
    }
}

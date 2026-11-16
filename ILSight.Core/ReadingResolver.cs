using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core;

/// <summary>A module's resolver as the library asks it within one unit: the module a reference names is handed out only
/// once it is added to the unit's reading, so it isn't released while the unit reads it; one closed is handed out as
/// not found. A type system's build also records what it was handed (<see cref="TypeSystemBuild{T}"/>), as values: it
/// holds nothing past the unit. A reference whose file is there but can't be read is read by name only, the failure
/// noted on the module (<see cref="ModuleLibrary.NoteUnreadReference"/>); one not found is read by name only and noted
/// as not found (<see cref="ModuleLibrary.NoteMissingReference"/>). The app's code the library calls back while it
/// reads: its work (the app's lookup of the file, handing it out, noting what it couldn't) marks
/// <see cref="Callbacks"/> when it throws, so the read's catch lets it through (CD4).</summary>
internal sealed class ReadingResolver : IAssemblyResolver
{
    private readonly ModuleLibrary _resolving;
    private readonly ModuleReading _reading;
    private readonly List<ModuleLibrary> _handedOut = [];

    public ReadingResolver(ModuleLibrary resolving, ModuleReading reading)
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

    public MetadataFile? Resolve(IAssemblyReference reference)
    {
        return Callbacks.Run((Resolver: this, reference), static s => s.Resolver.ResolveAssembly(s.reference));
    }

    private MetadataFile? ResolveAssembly(IAssemblyReference reference)
    {
        LoadedModule? found;
        try
        {
            found = _resolving.Resolver.ResolveAssembly(reference);
        }
        catch (AssemblyLoadException e)
        {
            return ReadByNameOnly(reference.Name, e);
        }

        return HandOut(found, reference.Name);
    }

    public MetadataFile? ResolveModule(MetadataFile mainModule, string moduleName)
    {
        return Callbacks.Run((Resolver: this, mainModule, moduleName), static s => s.Resolver.FindModule(s.mainModule, s.moduleName));
    }

    private MetadataFile? FindModule(MetadataFile mainModule, string moduleName)
    {
        LoadedModule? found;
        try
        {
            found = _resolving.Resolver.FindModule(mainModule, moduleName);
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

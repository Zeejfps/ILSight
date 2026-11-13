using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core;

/// <summary>A module's resolver as the library asks it within one unit: the module a reference names is handed out only
/// once it is added to the unit's reading, so it isn't released while the unit reads it; one closed is handed out as
/// not found. A type system's build also records what it was handed (<see cref="TypeSystemBuild{T}"/>), as values: it
/// holds nothing past the unit.</summary>
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
        return HandOut(_resolving.Resolver.ResolveAssembly(reference));
    }

    public MetadataFile? ResolveModule(MetadataFile mainModule, string moduleName)
    {
        return HandOut(_resolving.Resolver.FindModule(mainModule, moduleName));
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

    private MetadataFile? HandOut(LoadedModule? module)
    {
        if (module is null)
        {
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

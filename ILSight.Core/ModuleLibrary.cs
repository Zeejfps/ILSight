using System.Collections.Concurrent;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>The library's objects for a module that was read: the file, the resolver of what it references, and what
/// takes reading, read once whichever thread asks first. Kept by <see cref="LoadedAssemblies"/> against the
/// <see cref="LoadedModule"/> made with them, and read only by the I/O side, each unit under a reading
/// (<see cref="TryBeginReading"/>). It keeps the rule across the readings and the release: closed
/// (<see cref="Close"/>), it is released once no reading is open, by the close or the last reading's end, whichever
/// comes last, exactly once. Closed, it begins no new reading: a unit that holds one goes on reading it. Its type
/// systems keep the files of the modules its resolver handed out, which every unit using them reads under its own
/// reading, taken as it comes to use them (<see cref="TypeSystemBuild{T}"/>). Released, a WebCIL module's mapped file is
/// disposed (any thread may unmap it); a <see cref="PEFile"/>, read whole into memory, is never disposed: the collector
/// frees it once nothing reaches it.</summary>
internal sealed class ModuleLibrary
{
    private readonly Lock _sync = new();

    private readonly List<UnreadReference> _unreadReferences = [];

    private readonly List<string> _missingReferences = [];

    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _readings;

    private bool _isClosed;

    private bool _isReleased;

    public ModuleLibrary(MetadataFile file, AssemblyResolver resolver, Func<DebugSymbols?> readDebugSymbols, Func<ReferenceUsage> readReferenceUsage)
    {
        File = file;
        Resolver = resolver;
        TypeSystem = new TypeSystemReader(this);
        DebugSymbols = new Lazy<DebugSymbols?>(readDebugSymbols);
        ReferenceUsage = new Lazy<ReferenceUsage>(readReferenceUsage);
    }

    public MetadataFile File { get; }

    public AssemblyResolver Resolver { get; }

    public TypeSystemReader TypeSystem { get; }

    /// <summary>The assembly's portable PDB; null when it has none.</summary>
    public Lazy<DebugSymbols?> DebugSymbols { get; }

    /// <summary>What the assembly uses of the assemblies it references.</summary>
    public Lazy<ReferenceUsage> ReferenceUsage { get; }

    /// <summary>The type systems of the settings that turn language features on or off that
    /// <see cref="TypeSystem"/> models, each with the modules its build was handed.</summary>
    public ConcurrentDictionary<TypeSystemOptions, Lazy<TypeSystemBuild<DecompilerTypeSystem>>> TypeSystems { get; } = new();

    /// <summary>The references whose file was found but could not be read, each once by its name, in the order met: the
    /// type systems read them by name only (<see cref="ReadingResolver"/>).</summary>
    public IReadOnlyList<UnreadReference> ListUnreadReferences()
    {
        lock (_unreadReferences)
        {
            return [.. _unreadReferences];
        }
    }

    /// <summary>Notes that the file found for the reference <paramref name="name"/> could not be read, with
    /// <paramref name="error"/>; a reference noted already keeps its first failure.</summary>
    public void NoteUnreadReference(string name, AssemblyLoadException error)
    {
        lock (_unreadReferences)
        {
            if (_unreadReferences.Exists(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _unreadReferences.Add(new UnreadReference { Name = name, Module = Path.GetFileName(File.FileName), Error = error });
        }
    }

    /// <summary>The names of the references no file was found for, each once, in the order met: the type systems read
    /// them by name only (<see cref="ReadingResolver"/>). Not found is no failure; the code says it was read without
    /// them.</summary>
    public IReadOnlyList<string> ListMissingReferences()
    {
        lock (_missingReferences)
        {
            return [.. _missingReferences];
        }
    }

    /// <summary>Notes that no file was found for the reference <paramref name="name"/>; a name noted already is kept
    /// once.</summary>
    public void NoteMissingReference(string name)
    {
        lock (_missingReferences)
        {
            if (!_missingReferences.Exists(m => string.Equals(m, name, StringComparison.OrdinalIgnoreCase)))
            {
                _missingReferences.Add(name);
            }
        }
    }

    /// <summary>Completes once the library's objects are released.</summary>
    public Task Released => _released.Task;

    /// <summary>Whether the library's objects were released: nothing may read them any more.</summary>
    public bool IsReleased
    {
        get
        {
            lock (_sync)
            {
                return _isReleased;
            }
        }
    }

    /// <summary>Begins a reading, which <see cref="EndReading"/> ends; false once the module was closed. A unit that
    /// began one before the close goes on reading under it, what it reads nested included, and the release waits for the
    /// last of them.</summary>
    public bool TryBeginReading()
    {
        lock (_sync)
        {
            if (_isClosed)
            {
                return false;
            }

            _readings++;
            return true;
        }
    }

    /// <summary>Ends a reading <see cref="TryBeginReading"/> began; the last one of a closed module releases it.</summary>
    public void EndReading()
    {
        lock (_sync)
        {
            if (_readings == 0)
            {
                throw new InvalidOperationException("A reading of a module ended that was never begun.");
            }

            _readings--;
            if (!_isClosed || _readings > 0 || _isReleased)
            {
                return;
            }

            _isReleased = true;
        }

        Release();
    }

    /// <summary>Marks the module closed, as the list no longer holds it; released here when no reading is open, else
    /// by the last reading's end. Closing it again does nothing.</summary>
    public void Close()
    {
        lock (_sync)
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;
            if (_readings > 0)
            {
                return;
            }

            _isReleased = true;
        }

        Release();
    }

    private void Release()
    {
        if (File is WebCilFile webCil)
        {
            webCil.Dispose();
        }

        _released.TrySetResult();
    }
}

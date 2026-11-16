using System.Runtime.ExceptionServices;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>Reads a module's type system for one set of options once (the module's own, or one the decompiler's settings
/// choose when they turn language features on or off that the module's own models): the read begun by whoever asks first (the edge that reads it ahead of its first
/// use, or that first use), on the thread pool, and kept, so every asker takes the same outcome. A read cancelled before
/// it began is begun again by the next asker. The read runs under the lease of the unit that began it, which awaits it
/// to its end, and what its resolver hands out is added to that lease; each asker then reads the modules handed out
/// under its own (<see cref="TypeSystemBuild{T}"/>), and one of them closed drops the read, begun again in that unit,
/// resolving without it. Its resolver counts into a mark of its own, which its catch reads: a bug of the resolver's goes
/// on to every asker as the bug, never kept as the module's failure.</summary>
internal sealed class TypeSystemReader(ModuleLibrary library, TypeSystemOptions options)
{
    private readonly Lock _gate = new();
    private Task<TypeSystemBuild<TypeSystemRead>>? _read;

    /// <summary>The read's outcome, every module it was handed added to <paramref name="reading"/>, the unit's,
    /// which holds this module.</summary>
    public async Task<TypeSystemRead> ReadAsync(ModuleReadLease reading, CancellationToken ct)
    {
        while (true)
        {
            var read = JoinOrBeginAsync(reading, ct);
            var build = await read;
            if (build.TryReadHandedOut(reading))
            {
                return build.Built;
            }

            Drop(read);
        }
    }

    private Task<TypeSystemBuild<TypeSystemRead>> JoinOrBeginAsync(ModuleReadLease reading, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_read is null || _read.IsCanceled)
            {
                _read = Task.Run(() => Read(reading, ct), ct);
            }

            return _read;
        }
    }

    private void Drop(Task<TypeSystemBuild<TypeSystemRead>> read)
    {
        lock (_gate)
        {
            // Another unit may already have begun the next read.
            if (ReferenceEquals(_read, read))
            {
                _read = null;
            }
        }
    }

    private TypeSystemBuild<TypeSystemRead> Read(ModuleReadLease reading, CancellationToken ct)
    {
        var resolver = new ReadingResolver(library, reading);
        var read = Build(resolver, ct);
        return new TypeSystemBuild<TypeSystemRead> { Built = read, HandedOut = resolver.ListHandedOut() };
    }

    private TypeSystemRead Build(ReadingResolver resolver, CancellationToken ct)
    {
        try
        {
            return new TypeSystemRead.Read { TypeSystem = new DecompilerTypeSystem(library.File, resolver, options) };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        // The resolver's own work threw: the app's bug, not the library's failure on the module (CD4).
        catch (Exception e) when (!resolver.Callbacks.Failed)
        {
            return new TypeSystemRead.Unreadable { Thrown = ExceptionDispatchInfo.Capture(e) };
        }
    }
}

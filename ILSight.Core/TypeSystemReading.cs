using System.Runtime.ExceptionServices;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>The read of a module's type system: begun by whoever asks first (the edge that reads it ahead of its first
/// use, or that first use), on the thread pool, and kept, so every asker takes the same outcome. A read cancelled before
/// it began is begun again by the next asker. The read runs under the reading of the unit that began it, which awaits it
/// to its end, and what its resolver hands out is added to that reading; each asker then reads the modules handed out
/// under its own (<see cref="TypeSystemBuild{T}"/>), and one of them closed drops the read, begun again in that unit,
/// resolving without it.</summary>
internal sealed class TypeSystemReading(ModuleLibrary library)
{
    private readonly Lock _gate = new();
    private Task<TypeSystemBuild<TypeSystemRead>>? _read;

    /// <summary>The read's outcome, every module it was handed added to <paramref name="reading"/>, the unit's,
    /// which holds this module.</summary>
    public async Task<TypeSystemRead> ReadAsync(ModuleReading reading, CancellationToken ct)
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

    /// <summary>The read under way or done, else one begun under <paramref name="reading"/>.</summary>
    private Task<TypeSystemBuild<TypeSystemRead>> JoinOrBeginAsync(ModuleReading reading, CancellationToken ct)
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

    /// <summary>Forgets <paramref name="read"/>, unless another unit already began the next.</summary>
    private void Drop(Task<TypeSystemBuild<TypeSystemRead>> read)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_read, read))
            {
                _read = null;
            }
        }
    }

    private TypeSystemBuild<TypeSystemRead> Read(ModuleReading reading, CancellationToken ct)
    {
        var resolver = new ReadingResolver(library, reading);
        var read = Build(resolver, ct);
        return new TypeSystemBuild<TypeSystemRead> { Built = read, HandedOut = resolver.ListHandedOut() };
    }

    private TypeSystemRead Build(ReadingResolver resolver, CancellationToken ct)
    {
        var settings = Settings.Create();
        try
        {
            return new TypeSystemRead.Read { TypeSystem = new DecompilerTypeSystem(library.File, resolver, settings) };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new TypeSystemRead.Unreadable { Thrown = ExceptionDispatchInfo.Capture(e) };
        }
    }
}

using System.Runtime.ExceptionServices;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>The one read of a module's type system: begun by whoever asks first (the edge that reads it ahead of its
/// first use, or that first use), on the thread pool, and kept, so every asker takes the same outcome. A read
/// cancelled before it began is begun again by the next asker. The read is a reading of its module of its own
/// (<see cref="ModuleLibrary.TryBeginReading"/>), begun as it begins and ended as it ends.</summary>
internal sealed class TypeSystemReading(ModuleLibrary library)
{
    private readonly Lock _gate = new();
    private Task<TypeSystemRead>? _read;

    public Task<TypeSystemRead> ReadAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            if (_read is null || _read.IsCanceled)
            {
                _read = Task.Run(() => ReadUnderReading(ct), ct);
            }

            return _read;
        }
    }

    /// <summary>The read, on the thread pool, under a reading of its own, begun as the read begins: whoever asked holds
    /// one then, awaiting the read to its end, so the module can't have been released.</summary>
    private TypeSystemRead ReadUnderReading(CancellationToken ct)
    {
        if (!library.TryBeginReading())
        {
            ct.ThrowIfCancellationRequested();
            throw new InvalidOperationException("A module's type system was read after its release: whoever asks for it holds a reading of it.");
        }

        try
        {
            return Read(ct);
        }
        finally
        {
            library.EndReading();
        }
    }

    private TypeSystemRead Read(CancellationToken ct)
    {
        var settings = Settings.Create();
        try
        {
            return new TypeSystemRead.Read { TypeSystem = new DecompilerTypeSystem(library.File, library.Resolver, settings) };
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

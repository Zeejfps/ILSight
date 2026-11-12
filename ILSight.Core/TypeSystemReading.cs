using System.Runtime.ExceptionServices;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>The one read of a module's type system: begun by whoever asks first (the edge that reads it ahead of its
/// first use, or that first use), on the thread pool, and kept, so every asker takes the same outcome. A read
/// cancelled before it began is begun again by the next asker.</summary>
internal sealed class TypeSystemReading(MetadataFile file, AssemblyResolver resolver)
{
    private readonly Lock _gate = new();
    private Task<TypeSystemRead>? _read;

    public Task<TypeSystemRead> ReadAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            if (_read is null || _read.IsCanceled)
            {
                _read = Task.Run(() => Read(ct), ct);
            }

            return _read;
        }
    }

    private TypeSystemRead Read(CancellationToken ct)
    {
        var settings = Settings.Create();
        try
        {
            return new TypeSystemRead.Read { TypeSystem = new DecompilerTypeSystem(file, resolver, settings) };
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

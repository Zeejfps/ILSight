namespace ILSight.Tests;

/// <summary>A read the tests share: started by the first test that awaits it, the same task for every test after,
/// its failure theirs too.</summary>
internal sealed class SharedRead<T>(Func<Task<T>> read)
{
    private readonly Lock _gate = new();
    private Task<T>? _started;

    public Task<T> ReadAsync()
    {
        lock (_gate)
        {
            _started ??= read();
            return _started;
        }
    }
}

using ILSight.Core;

namespace ILSight.Tests;

/// <summary>What a unit over a module the test keeps open read: a module it never closes is never released, so a
/// closed answer fails the test.</summary>
public static class ModuleReadQueries
{
    public static T GetValue<T>(this ModuleRead<T> read)
    {
        return read.Match(
            static r => r.Value,
            static c => throw new InvalidOperationException($"'{c.Module.FilePath}' was released, which the test never closed."));
    }

    public static async Task<T> GetValueAsync<T>(this Task<ModuleRead<T>> read)
    {
        return (await read).GetValue();
    }
}

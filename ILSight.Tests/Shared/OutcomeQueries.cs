using ILSight.Core;

namespace ILSight.Tests;

/// <summary>What a read the test expects the library to manage gave: a failed one fails the test, with what it failed
/// with.</summary>
public static class OutcomeQueries
{
    public static T GetValue<T>(this Outcome<T> outcome)
    {
        return outcome.Match(
            static ok => ok.Value,
            static failed => throw new InvalidOperationException($"The read failed, which the test expected the library to manage: {failed.Error.Message}",
                failed.Error));
    }

    public static async Task<T> GetValueAsync<T>(this Task<Outcome<T>> read)
    {
        return (await read).GetValue();
    }

    /// <summary>What a unit over a module the test keeps open read (<see cref="ModuleReadQueries"/>), which the library
    /// managed to read.</summary>
    public static async Task<T> GetValueAsync<T>(this Task<Outcome<ModuleRead<T>>> read)
    {
        return (await read).GetValue().GetValue();
    }
}

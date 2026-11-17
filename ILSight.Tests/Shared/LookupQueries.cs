using ILSight.Core;

namespace ILSight.Tests;

/// <summary>What a lookup found, read as the tests read a value that may be missing: the value, or null when it found
/// nothing. A lookup that met a module whose type system could not be read fails the test.</summary>
internal static class LookupQueries
{
    extension<T>(Lookup<T> lookup) where T : class
    {
        public T? FoundOrNull
        {
            get
            {
                return lookup switch
                {
                    Lookup<T>.Found found => found.Value,
                    Lookup<T>.NotFound => null,
                    Lookup<T>.Unreadable unreadable => throw CreateUnreadableFailure(unreadable),
                };
            }
        }
    }

    extension<T>(Lookup<T> lookup) where T : struct
    {
        public T? FoundValueOrNull
        {
            get
            {
                return lookup switch
                {
                    Lookup<T>.Found found => found.Value,
                    Lookup<T>.NotFound => null,
                    Lookup<T>.Unreadable unreadable => throw CreateUnreadableFailure(unreadable),
                };
            }
        }
    }

    private static InvalidOperationException CreateUnreadableFailure<T>(Lookup<T>.Unreadable unreadable) where T : notnull
    {
        return new InvalidOperationException(
            $"The lookup met a module whose type system could not be read, which the test expected the library to read: {unreadable.Module.Why.Message}",
            unreadable.Module.Why);
    }
}

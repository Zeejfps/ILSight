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
                Assert.IsNotType<Lookup<T>.Unreadable>(lookup);
                if (lookup is Lookup<T>.Found found)
                {
                    return found.Value;
                }

                return null;
            }
        }
    }

    extension<T>(Lookup<T> lookup) where T : struct
    {
        public T? FoundValueOrNull
        {
            get
            {
                Assert.IsNotType<Lookup<T>.Unreadable>(lookup);
                if (lookup is Lookup<T>.Found found)
                {
                    return found.Value;
                }

                return null;
            }
        }
    }
}

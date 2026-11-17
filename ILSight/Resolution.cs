using ILSight.Core;

namespace ILSight;

/// <summary>What finding a place for a request came to: found, not there, in a module whose type system the library
/// could not read, failed, or stale because the user has asked for something else since.</summary>
public union Resolution<T>(Resolution<T>.Found, Resolution<T>.Missing, Resolution<T>.Unreadable, Resolution<T>.Failed,
    Resolution<T>.Stale)
{
    public sealed record Found(T Value);

    /// <summary>The place is not there.</summary>
    public sealed record Missing;

    /// <summary>The place is in <see cref="Module"/>, whose type system the library could not read, so it was not
    /// looked for.</summary>
    public sealed record Unreadable
    {
        public required UnreadableModule Module { get; init; }
    }

    public sealed record Failed(Exception Error);

    /// <summary>Another request began while this one was resolving: nothing is to be shown or reported.</summary>
    public sealed record Stale;
}

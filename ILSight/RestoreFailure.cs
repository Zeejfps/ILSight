using System.Diagnostics;

namespace ILSight;

/// <summary>Why a root restored from the saved analyses could not be analyzed again, as its row says: its symbol's
/// module could not be read, its symbol is not in its file, or looking the saved analyses up failed.</summary>
public abstract record RestoreFailure
{
    private RestoreFailure()
    {
    }

    /// <summary>The library could not read the module the symbol is in.</summary>
    public sealed record Unreadable : RestoreFailure
    {
        /// <summary>The module's file name.</summary>
        public required string Name { get; init; }

        public required string Why { get; init; }
    }

    /// <summary>The symbol is not in its file as it was read.</summary>
    public sealed record NotFound : RestoreFailure;

    /// <summary>Looking the saved analyses up failed.</summary>
    public sealed record Failed : RestoreFailure
    {
        public required string Why { get; init; }
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Unreadable, TResult> unreadable, Func<NotFound, TResult> notFound, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Unreadable c => unreadable(c),
            NotFound c => notFound(c),
            Failed c => failed(c),
            _ => throw new UnreachableException($"RestoreFailure.Match does not handle {GetType().Name}."),
        };
    }
}

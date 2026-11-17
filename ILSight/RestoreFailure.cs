namespace ILSight;

/// <summary>Why a root restored from the saved analyses could not be analyzed again, as its row says: its symbol's
/// module could not be read, its symbol is not in its file, or looking the saved analyses up failed.</summary>
public union RestoreFailure(RestoreFailure.Unreadable, RestoreFailure.NotFound, RestoreFailure.Failed)
{
    /// <summary>The library could not read the module the symbol is in.</summary>
    public sealed record Unreadable
    {
        /// <summary>The module's file name.</summary>
        public required string Name { get; init; }

        public required string Why { get; init; }
    }

    /// <summary>The symbol is not in its file as it was read.</summary>
    public sealed record NotFound;

    /// <summary>Looking the saved analyses up failed, with <see cref="Error"/>.</summary>
    public sealed record Failed
    {
        public required Exception Error { get; init; }
    }
}

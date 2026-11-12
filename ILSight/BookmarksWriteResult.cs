using System.Diagnostics;

namespace ILSight;

/// <summary>What writing a bookmarks file did.</summary>
public abstract record BookmarksWriteResult
{
    private BookmarksWriteResult()
    {
    }

    public sealed record Written : BookmarksWriteResult;

    /// <summary><see cref="Reason"/> names the file and says why, as the system said it.</summary>
    public sealed record Failed : BookmarksWriteResult
    {
        public required string Reason { get; init; }
    }

    public TResult Match<TResult>(Func<Written, TResult> written, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Written w => written(w),
            Failed f => failed(f),
            _ => throw new UnreachableException($"BookmarksWriteResult.Match does not handle {GetType().Name}."),
        };
    }
}

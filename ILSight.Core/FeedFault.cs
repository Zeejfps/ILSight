using System.Diagnostics;

namespace ILSight.Core;

/// <summary>Why a NuGet feed's answer can't be used, as ILSight's own reader of the protocol finds it
/// (<see cref="NuGetFeeds"/>, <see cref="FeedJson"/>): a cause the app words in each language. Which field or address
/// is wrong is the diagnostic, carried by the exception's message.</summary>
public abstract record FeedFault
{
    private FeedFault()
    {
    }

    /// <summary>The feed answered with something that is not JSON.</summary>
    public sealed record NotJson : FeedFault;

    /// <summary>The feed's service index names no resource of <see cref="Type"/>, which the request needs.</summary>
    public sealed record ResourceMissing(string Type) : FeedFault;

    /// <summary>The answer is JSON, but not in the form the protocol gives it: a list, a value or an address missing or of
    /// the wrong kind.</summary>
    public sealed record Malformed : FeedFault;

    public TResult Match<TResult>(Func<NotJson, TResult> notJson, Func<ResourceMissing, TResult> resourceMissing, Func<Malformed, TResult> malformed)
    {
        return this switch
        {
            NotJson n => notJson(n),
            ResourceMissing r => resourceMissing(r),
            Malformed m => malformed(m),
            _ => throw new UnreachableException($"FeedFault.Match does not handle {GetType().Name}."),
        };
    }
}

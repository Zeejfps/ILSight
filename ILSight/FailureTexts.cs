using ILSight.Core;

namespace ILSight;

/// <summary>What failed, said from its cause in the app's language where the failure carries one of ILSight's own
/// (a bundle's file it can't read, a NuGet feed's answer it can't use); any other failure, known only by its exception,
/// by its message as written.</summary>
public static class FailureTexts
{
    /// <summary>The cause of <paramref name="error"/>, as a sentence.</summary>
    public static string DescribeCause(Strings t, Exception error)
    {
        return error switch
        {
            BundleEntryException bundle => DescribeBundleEntry(t, bundle.Fault),
            FeedFaultException feed => DescribeFeed(t, feed.Fault),
            _ => error.Message,
        };
    }

    private static string DescribeFeed(Strings t, FeedFault fault)
    {
        return fault.Match(
            _ => t.CauseFeedNotJsonSentence,
            r => t.CauseFeedResourceMissingSentence(resource: OneLineText.Name(r.Type)),
            _ => t.CauseFeedMalformedSentence);
    }

    private static string DescribeBundleEntry(Strings t, BundleEntryFault fault)
    {
        var entry = OneLineText.Name(fault.Entry);
        return fault.Match(
            _ => t.CauseBundleEntryOutsideSentence(entry: entry),
            _ => t.CauseBundleEntrySizeOutOfRangeSentence(entry: entry),
            _ => t.CauseBundleEntryInflatesWrongSentence(entry: entry));
    }
}

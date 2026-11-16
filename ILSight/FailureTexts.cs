using ILSight.Core;
using ILSight.Core.ResourceArea;

namespace ILSight;

/// <summary>What failed, said from its cause in the app's language where the failure carries one of ILSight's own
/// (a file it can't open as an assembly, a bundle's file it can't read, a NuGet feed's answer it can't use, an Avalonia
/// container it can't list, a request that timed out); any other failure, known only by its exception, by its message
/// as written.</summary>
public static class FailureTexts
{
    /// <summary>The cause of <paramref name="error"/>, as a sentence.</summary>
    public static string DescribeCause(Strings t, Exception error)
    {
        return error switch
        {
            AssemblyLoadException load => DescribeAssemblyLoad(t, load),
            BundleEntryException bundle => DescribeBundleEntry(t, bundle.Fault),
            FeedFaultException feed => DescribeFeed(t, feed.Fault),
            AvaloniaContainerException container => DescribeAvaloniaContainer(t, container.Fault),
            TimeoutException => t.CauseRequestTimedOutSentence,
            _ => error.Message,
        };
    }

    /// <summary>Why the file of <paramref name="error"/> couldn't be opened, where a sentence names the file already:
    /// what the system or the library said, as <see cref="DescribeCause"/> says it; else the loader's own cause.</summary>
    public static string DescribeUnopened(Strings t, AssemblyLoadException error)
    {
        var why = error.InnerException;
        if (why is null || error.Fault is not AssemblyLoadFault.CannotOpen)
        {
            return DescribeAssemblyLoad(t, error);
        }

        return DescribeCause(t, why);
    }

    // The file's cause in the language's words; where the system or the library said why it couldn't be opened, that
    // cause too, said as DescribeCause says it, joined by the language's template.
    private static string DescribeAssemblyLoad(Strings t, AssemblyLoadException load)
    {
        var file = OneLineText.Name(load.FilePath.Text);
        return load.Fault.Match(
            _ => DescribeCannotOpen(t, load, file),
            _ => t.CauseAssemblyNotDotnetSentence(file: file),
            p => t.CauseAssemblyNotPackageSentence(file: OneLineText.Name(p.Container.Text)),
            i => t.CauseAssemblyNotInPackageSentence(package: OneLineText.Name(i.Package.Text), name: OneLineText.Name(i.FullName)));
    }

    // The loader makes every such failure with what the system or the library threw; one made without it has only its
    // diagnostic to say.
    private static string DescribeCannotOpen(Strings t, AssemblyLoadException load, string file)
    {
        var why = load.InnerException;
        if (why is null)
        {
            return load.Message;
        }

        return t.CauseAssemblyCannotOpenSentence(file: file, why: DescribeCause(t, why));
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

    private static string DescribeAvaloniaContainer(Strings t, AvaloniaContainerFault fault)
    {
        return fault.Match(
            v => t.CauseAvaloniaIndexVersionUnknownSentence(container: AvaloniaContainerFault.ContainerName, version: v.Version),
            i => t.CauseAvaloniaIndexEntryIncompleteSentence(container: AvaloniaContainerFault.ContainerName, field: i.Field),
            o => t.CauseAvaloniaEntryOutsideSentence(path: OneLineText.Name(o.Path), container: AvaloniaContainerFault.ContainerName));
    }
}

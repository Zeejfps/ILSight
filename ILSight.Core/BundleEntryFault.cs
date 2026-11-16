using System.Diagnostics;

namespace ILSight.Core;

/// <summary>Why a file of a single-file bundle can't be read, as ILSight's own reader of bundles finds it
/// (<see cref="BundleContents"/>): a cause the app words in each language. Which offset or size is wrong is the
/// diagnostic, carried by the exception's message.</summary>
public abstract record BundleEntryFault
{
    private BundleEntryFault()
    {
    }

    /// <summary>The path of the file in the bundle.</summary>
    public required string Entry { get; init; }

    /// <summary>The manifest places the file before the start of the bundle, or past its end.</summary>
    public sealed record Outside : BundleEntryFault;

    /// <summary>The manifest gives the file a size below zero, or too large to read.</summary>
    public sealed record SizeOutOfRange : BundleEntryFault;

    /// <summary>The file, stored compressed, inflates to a size other than the manifest declares.</summary>
    public sealed record InflatesWrong : BundleEntryFault;

    public TResult Match<TResult>(Func<Outside, TResult> outside, Func<SizeOutOfRange, TResult> sizeOutOfRange, Func<InflatesWrong, TResult> inflatesWrong)
    {
        return this switch
        {
            Outside o => outside(o),
            SizeOutOfRange s => sizeOutOfRange(s),
            InflatesWrong i => inflatesWrong(i),
            _ => throw new UnreachableException($"BundleEntryFault.Match does not handle {GetType().Name}."),
        };
    }
}

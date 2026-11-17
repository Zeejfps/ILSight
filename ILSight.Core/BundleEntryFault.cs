namespace ILSight.Core;

/// <summary>Why a file of a single-file bundle can't be read, as ILSight's own reader of bundles finds it
/// (<see cref="BundleContents"/>): a cause the app words in each language. Which offset or size is wrong is the
/// diagnostic, carried by the exception's message.</summary>
public union BundleEntryFault(BundleEntryFault.Outside, BundleEntryFault.SizeOutOfRange, BundleEntryFault.InflatesWrong)
{
    /// <summary>The manifest places the file before the start of the bundle, or past its end.</summary>
    public sealed record Outside
    {
        /// <summary>The path of the file in the bundle.</summary>
        public required string Entry { get; init; }
    }

    /// <summary>The manifest gives the file a size below zero, or too large to read.</summary>
    public sealed record SizeOutOfRange
    {
        /// <summary>The path of the file in the bundle.</summary>
        public required string Entry { get; init; }
    }

    /// <summary>The file, stored compressed, inflates to a size other than the manifest declares.</summary>
    public sealed record InflatesWrong
    {
        /// <summary>The path of the file in the bundle.</summary>
        public required string Entry { get; init; }
    }

    /// <summary>The path of the file in the bundle.</summary>
    public string Entry => this switch
    {
        Outside outside => outside.Entry,
        SizeOutOfRange sizeOutOfRange => sizeOutOfRange.Entry,
        InflatesWrong inflatesWrong => inflatesWrong.Entry,
    };
}

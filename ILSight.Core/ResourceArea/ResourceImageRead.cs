namespace ILSight.Core.ResourceArea;

/// <summary>What reading a picture came to: the picture, or why it can't be read.</summary>
public union ResourceImageRead(ResourceImageRead.Decoded, ResourceImageRead.Unreadable)
{
    public sealed record Decoded
    {
        public required ResourceImage Image { get; init; }
    }

    /// <summary>No picture these decoders know, or one they fail on.</summary>
    public sealed record Unreadable
    {
        /// <summary>Why, the cause the app words in each language.</summary>
        public required PictureFault Fault { get; init; }

        /// <summary>What the decoder threw, so its record has the diagnostic (CF3); where ILSight's own check of the bytes
        /// decided it, the <see cref="PictureFaultException"/> its decoders say bad data by, its message in English.</summary>
        public required Exception Error { get; init; }
    }
}

namespace ILSight.Core.MetadataArea;

/// <summary>What is shown under the selected row of a <see cref="MetadataGrid"/>.</summary>
public union MetadataRowDetails(MetadataRowDetails.Text, MetadataRowDetails.Table, MetadataRowDetails.Failed)
{
    /// <summary>A decoded text, with the file extension of the language it is in, if known.</summary>
    public sealed record Text
    {
        public required string Content { get; init; }

        public required string? Extension { get; init; }
    }

    /// <summary>A decoded list, as a table.</summary>
    public sealed record Table
    {
        public required IReadOnlyList<string> Headers { get; init; }

        public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }
    }

    /// <summary>Why the row's details could not be read, and what the library threw reading them, so its record has the
    /// diagnostic (CF3).</summary>
    public sealed record Failed
    {
        public required string Reason { get; init; }

        public required Exception Error { get; init; }
    }
}

namespace ILSight;

/// <summary>A problem in words: a one-line <see cref="Title"/> and the full <see cref="Details"/>, e.g. an
/// exception with its stack trace.</summary>
public sealed record ProblemText(string Title, string Details)
{
    /// <summary>The title and details as one text, for copying.</summary>
    public string FullText { get; } = Details.Length == 0 ? Title : $"{Title}{Environment.NewLine}{Environment.NewLine}{Details}";

    /// <summary>The title and the first line of the details on one line, for a tooltip.</summary>
    public string Summary { get; } = ProblemTexts.Summarize(Title, Details);
}

namespace ILSight;

/// <summary>A problem in words: a one-line <see cref="Title"/> and the full <see cref="Details"/>, e.g. an
/// exception with its stack trace; and the two as one text, and on one line, made with them.</summary>
public sealed record ProblemText
{
    private ProblemText(string title, string details)
    {
        Title = title;
        Details = details;
        FullText = details.Length == 0 ? title : $"{title}{Environment.NewLine}{Environment.NewLine}{details}";
        Summary = ProblemTexts.Summarize(title, details);
    }

    public string Title { get; }

    public string Details { get; }

    /// <summary>The title and details as one text, for copying.</summary>
    public string FullText { get; }

    /// <summary>The title and the first line of the details on one line, for a tooltip.</summary>
    public string Summary { get; }

    /// <summary>The problem titled <paramref name="title"/>, told in full by <paramref name="details"/>.</summary>
    public static ProblemText FromWords(string title, string details)
    {
        return new ProblemText(title, details);
    }
}

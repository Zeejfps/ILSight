namespace ILSight;

/// <summary>A problem in words: a one-line <see cref="Title"/> and the <see cref="Details"/> that say its cause and
/// recovery; apart from them, the <see cref="TechnicalDetails"/> (an exception as the runtime wrote it, with its stack
/// trace), never in the text the user reads unless they ask for it; and the whole as one text, and the title and
/// details on one line, made with them.</summary>
public sealed record ProblemText
{
    private ProblemText(Strings t, string title, string details, string technicalDetails)
    {
        Title = title;
        Details = details;
        TechnicalDetails = technicalDetails;
        FullText = Join(Join(title, details), technicalDetails.Length == 0 ? "" : t.ErrorsTechnicalDetails + Environment.NewLine + technicalDetails);
        Summary = ProblemTexts.Summarize(t, title, details);
    }

    public string Title { get; }

    /// <summary>The cause, by its message, and the recovery: no exception's type name or stack trace.</summary>
    public string Details { get; }

    /// <summary>The exception behind the problem as the runtime wrote it (its type, message, inner exceptions and
    /// stack trace), in no app language; empty when there is none.</summary>
    public string TechnicalDetails { get; }

    /// <summary>The title, the details and the technical details under their label, as one text, for copying.</summary>
    public string FullText { get; }

    /// <summary>The title and the first line of the details on one line, for a tooltip.</summary>
    public string Summary { get; }

    /// <summary>The problem titled <paramref name="title"/>, said by <paramref name="details"/>, in
    /// <paramref name="t"/>'s language, with <paramref name="technicalDetails"/> apart.</summary>
    public static ProblemText FromWords(Strings t, string title, string details, string technicalDetails)
    {
        return new ProblemText(t, title, details, technicalDetails);
    }

    // Parts on lines of their own, a blank line between: layout, the same in every language.
    private static string Join(string first, string second)
    {
        return second.Length == 0 ? first : $"{first}{Environment.NewLine}{Environment.NewLine}{second}";
    }
}

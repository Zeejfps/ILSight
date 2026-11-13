using ILSight.Core;

namespace ILSight;

/// <summary>What each error says, put into the words of a language once, and the document of an error's text, kept
/// for as long as that language lasts.</summary>
public sealed class ErrorTexts
{
    private Memo<ByReference<Strings>, Dictionary<AppError, ProblemText>> _texts;
    private Memo<ByReference<AppError>, DecompiledDocument> _document;

    /// <summary><paramref name="error"/> in the words of <paramref name="t"/>, put into them once per language.</summary>
    public ProblemText GetText(AppError error, Strings t)
    {
        if (!_texts.TryGet(new ByReference<Strings>(t), out var texts))
        {
            texts = new Dictionary<AppError, ProblemText>(ReferenceEqualityComparer.Instance);
            _texts = new Memo<ByReference<Strings>, Dictionary<AppError, ProblemText>>(new ByReference<Strings>(t), texts);
            _document = default;
        }
        if (!texts.TryGetValue(error, out var text))
        {
            text = ProblemTexts.Describe(t, error.Problem);
            texts[error] = text;
        }
        return text;
    }

    /// <summary><paramref name="error"/>'s details, or its title when it has none, in the words of
    /// <paramref name="t"/>, as a document for a code view.</summary>
    public DecompiledDocument GetDocument(AppError error, Strings t)
    {
        var text = GetText(error, t);
        if (_document.TryGet(new ByReference<AppError>(error), out var document))
        {
            return document;
        }

        document = CreateDocument(text);
        _document = new Memo<ByReference<AppError>, DecompiledDocument>(new ByReference<AppError>(error), document);
        return document;
    }

    private static DecompiledDocument CreateDocument(ProblemText text)
    {
        var body = text.Details;
        if (body.Length == 0)
        {
            body = text.Title;
        }

        return Documents.CreateFromText(text.Title, body);
    }
}

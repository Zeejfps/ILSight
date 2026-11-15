using ILSight.Core;

namespace ILSight;

/// <summary>The code a tab's code view shows, its notes in the interface's language: the document as it was made, its
/// notes found again from their causes (<see cref="Documents.Reword"/>) only when the document or the language
/// changes, the code never decompiled again.</summary>
public sealed class ShownCode
{
    private Memo<(ByReference<DecompiledDocument>, ByReference<Strings>), DecompiledDocument> _worded;

    /// <summary><paramref name="document"/> with its notes in <paramref name="t"/>'s language.</summary>
    public DecompiledDocument GetDocument(DecompiledDocument document, Strings t)
    {
        if (document.Notes.IsEmpty)
        {
            return document;
        }

        var key = (new ByReference<DecompiledDocument>(document), new ByReference<Strings>(t));
        if (_worded.TryGet(key, out var worded))
        {
            return worded;
        }

        worded = Documents.Reword(document, new DocumentNoteTexts(t));
        _worded = new Memo<(ByReference<DecompiledDocument>, ByReference<Strings>), DecompiledDocument>(key, worded);
        return worded;
    }
}

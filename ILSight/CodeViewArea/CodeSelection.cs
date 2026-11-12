using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>The selection in a code view, whose triple press selects the row there: a line, or a
/// collapsed fold with the lines it hides.</summary>
public sealed class CodeSelection : TextSelection<DecompiledDocument, TextPosition>
{
    /// <summary>The folds of the view this selection belongs to, which say what a block is. The selection
    /// owns them as it has to read them and is made with the view; see <see cref="CodeViewState.Folds"/>.</summary>
    internal readonly CodeFolds Folds = new();

    protected override (TextPosition Start, TextPosition End) WordAt(DecompiledDocument document, TextPosition at)
    {
        return CodeText.WordAt(document, at);
    }

    protected override (TextPosition Start, TextPosition End) BlockAt(DecompiledDocument document, TextPosition at)
    {
        var first = new TextPosition { Line = CodeFolding.GetVisibleLine(Folds, at.Line), Column = 0 };
        var next = CodeFolding.GetNextLine(Folds, first.Line);
        if (next >= document.Lines.Length)
        {
            return (first, CodeText.End(document));
        }

        return (first, new TextPosition { Line = next, Column = 0 });
    }
}

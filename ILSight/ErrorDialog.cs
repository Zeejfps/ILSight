using ILSight.Core;
using ILSight.CodeView;

namespace ILSight;

/// <summary>The error dialog: whether it shows, the error picked in it, and the view of that error's text.</summary>
public sealed class ErrorDialog
{
    /// <summary>What each error says, put into those words once, by the language they are in.</summary>
    public Memo<ByReference<Strings>, Dictionary<AppError, ProblemText>> Texts;

    /// <summary>The document of an error's text, by the error, for as long as the language lasts.</summary>
    public Memo<ByReference<AppError>, DecompiledDocument> Document;

    /// <summary>The error log's revision when the dialog last saw it.</summary>
    public int ShownRevision;

    public bool IsOpen;

    /// <summary>The error the dialog shows.</summary>
    public ItemSelection<AppError> Selection { get; } = new();

    public AppError? SelectedError => Selection.Item;

    /// <summary>The view of <see cref="SelectedError"/>'s text.</summary>
    public CodeViewState View { get; } = new();
}

using ILSight.Core;
using ILSight.Localization;

namespace ILSight;

/// <summary>The error dialog: whether it shows, the error picked in it, and the view of that error's text.</summary>
public sealed class ErrorDialog
{
    /// <summary>What each error says in <see cref="TextsIn"/>, put into those words once.</summary>
    public readonly Dictionary<AppError, ProblemText> Texts = new(ReferenceEqualityComparer.Instance);
    public Strings? TextsIn;

    /// <summary>The error <see cref="Document"/> shows the text of.</summary>
    public AppError? DocumentFor;
    public DecompiledDocument Document = DecompiledDocument.Empty;

    /// <summary>The error log's revision when the dialog last saw it.</summary>
    public int ShownRevision;

    public bool IsOpen;

    /// <summary>The error the dialog shows.</summary>
    public ItemSelection<AppError> Selection { get; } = new();

    public AppError? SelectedError => Selection.Item;

    /// <summary>The view of <see cref="SelectedError"/>'s text.</summary>
    public CodeViewState View { get; } = new();
}

using ILSight.CodeViewArea;

namespace ILSight;

/// <summary>The error dialog: whether it shows, the error picked in it, and the view of that error's text.</summary>
public sealed class ErrorDialog
{
    public bool IsOpen;

    public ErrorTexts Texts { get; } = new();

    /// <summary>The error the dialog shows.</summary>
    public ItemSelection<AppError> Selection { get; } = new();

    public AppError? SelectedError => Selection.Item;

    /// <summary>The view of <see cref="SelectedError"/>'s text.</summary>
    public CodeViewState View { get; } = new();

    /// <summary>Opens the dialog on the newest of <paramref name="errors"/>, unless there are none.</summary>
    public void Open(IReadOnlyList<AppError> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        Selection.Select(errors[^1], errors);
        IsOpen = true;
    }
}

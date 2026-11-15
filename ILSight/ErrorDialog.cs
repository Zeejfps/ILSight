using ILSight.CodeViewArea;

namespace ILSight;

/// <summary>The error dialog: whether it shows, the error picked in it, and the view of that error's text.</summary>
public sealed class ErrorDialog
{
    public bool IsOpen;

    /// <summary>Whether the view shows the picked error's technical details (its exception as the runtime wrote it)
    /// instead of its details: off each time the dialog opens, so the user opens them on purpose.</summary>
    public bool ShowsTechnicalDetails;

    public ErrorTexts Texts { get; } = new();

    /// <summary>The error the dialog shows.</summary>
    public ItemSelection<AppError> Selection { get; } = new();

    /// <summary>The view of selected error's text.</summary>
    public CodeViewState View { get; } = new();

    /// <summary>Opens the dialog on the newest of <paramref name="errors"/>, unless there are none.</summary>
    public void Open(IReadOnlyList<AppError> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        Selection.Select(errors[^1], errors);
        ShowsTechnicalDetails = false;
        IsOpen = true;
    }
}

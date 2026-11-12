namespace ILSight;

/// <summary>What the assembly lists dialog is doing: closed; open on the lists; renaming one, with the name
/// typed so far; or asking whether to delete one.</summary>
public abstract record ListsDialogMode
{
    private ListsDialogMode()
    {
    }

    public sealed record Closed : ListsDialogMode;

    public sealed record Browsing : ListsDialogMode;

    public sealed record Renaming(string List) : ListsDialogMode
    {
        /// <summary>A field, for the edit box to take by <c>ref</c>.</summary>
        public string NewName = List;

        /// <summary>Whether the name box is still to take focus, with the whole name selected.</summary>
        public bool IsFocusPending = true;
    }

    public sealed record ConfirmingDelete(string List) : ListsDialogMode;
}

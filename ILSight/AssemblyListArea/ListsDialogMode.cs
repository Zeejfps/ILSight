namespace ILSight.AssemblyListArea;

/// <summary>What the assembly lists dialog is doing: closed; open on the lists; renaming one, with the name
/// typed so far; or asking whether to delete one.</summary>
public union ListsDialogMode(ListsDialogMode.Closed, ListsDialogMode.Browsing, ListsDialogMode.Renaming,
    ListsDialogMode.ConfirmingDelete)
{
    public sealed record Closed;

    public sealed record Browsing;

    public sealed record Renaming(string List)
    {
        /// <summary>A field, for the edit box to take by <c>ref</c>.</summary>
        public string NewName = List;

        private bool _isFocusPending = true;

        /// <summary>Whether the name box is to take focus, with the whole name selected: true the first
        /// time it is asked only.</summary>
        public bool TryTakeFocus()
        {
            var focus = _isFocusPending;
            _isFocusPending = false;
            return focus;
        }
    }

    public sealed record ConfirmingDelete(string List);
}

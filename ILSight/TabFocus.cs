namespace ILSight;

/// <summary>How the screen follows the app state's active tab: the tab it last followed, which the views open beside,
/// and its last try at focusing another tab while none was active.</summary>
public sealed class TabFocus
{
    private TabFocusTry _lastTry = new TabFocusTry { LayoutChanges = -1, TabCount = -1 };

    public DocumentTab? Active { get; private set; }

    public void Follow(DocumentTab? active)
    {
        Active = active;
    }

    /// <summary>Records <paramref name="attempt"/> as the last try; false when it is that try again, which the tabs and
    /// the layout, unchanged since, would answer as before.</summary>
    public bool TryNewAttempt(TabFocusTry attempt)
    {
        if (attempt == _lastTry)
        {
            return false;
        }

        _lastTry = attempt;
        return true;
    }
}

namespace ILSight.MetadataGridArea;

/// <summary>A grid's flyout of flags: whether it shows, a field its widget takes by <c>ref</c>, and the column it was
/// last opened for.</summary>
internal sealed class FlagsFlyout
{
    public bool IsOpen;

    /// <summary>The column the flyout was last opened for, or <see cref="MetadataGridState.None"/>.</summary>
    public int Column { get; private set; } = MetadataGridState.None;

    public bool IsOpenFor(int column)
    {
        return IsOpen && Column == column;
    }

    /// <summary>Opens the flyout for <paramref name="column"/>, or closes it when it shows that column's.</summary>
    public void Toggle(int column)
    {
        IsOpen = !IsOpenFor(column);
        Column = column;
    }
}

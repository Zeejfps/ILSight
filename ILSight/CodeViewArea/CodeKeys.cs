namespace ILSight.CodeViewArea;

/// <summary>What the keys did in a code view that later keys and the scroll take up: the column the caret keeps to as
/// Up and Down move it, and the rows the keys asked to scroll since the view was last laid out.</summary>
internal sealed class CodeKeys
{
    private int _scrollRows;

    /// <summary>The column the caret keeps to while Up and Down move it from where they left it; null before.</summary>
    public CaretGoal? Goal { get; private set; }

    public void AimAt(CaretGoal goal)
    {
        Goal = goal;
    }

    /// <summary>Asks to scroll <paramref name="rows"/> rows, up when negative.</summary>
    public void ScrollRows(int rows)
    {
        _scrollRows += rows;
    }

    /// <summary>The rows asked to scroll since the last call.</summary>
    public int TakeScrollRows()
    {
        var rows = _scrollRows;
        _scrollRows = 0;
        return rows;
    }

    /// <summary>Forgets the goal and the rows asked for, as a new document does.</summary>
    public void Forget()
    {
        _scrollRows = 0;
        Goal = null;
    }
}

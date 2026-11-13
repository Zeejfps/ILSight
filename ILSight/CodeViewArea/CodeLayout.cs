using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>The document a code view shows, and how the view was last laid out: its rows' height, where it was
/// scrolled to, and how many rows Page Up and Page Down move.</summary>
internal sealed class CodeLayout
{
    /// <summary>The document shown; null before the first.</summary>
    public DecompiledDocument? Document { get; private set; }

    /// <summary>How many columns the longest line of <see cref="Document"/> takes.</summary>
    public int MaxColumns { get; private set; }

    public float RowHeight { get; private set; }

    public float ScrollX { get; private set; }

    public float ScrollY { get; private set; }

    /// <summary>How many rows Page Up and Page Down move, a view less one.</summary>
    public int PageRows { get; private set; } = 1;

    /// <summary>Takes <paramref name="document"/> as the one shown, scrolled to its start.</summary>
    public void Show(DecompiledDocument document)
    {
        Document = document;
        MaxColumns = CodeText.CalculateMaxColumns(document);
        ScrollX = 0f;
        ScrollY = 0f;
    }

    public void TakeRowHeight(float rowHeight)
    {
        RowHeight = rowHeight;
    }

    /// <summary>Takes a view <paramref name="viewport"/> tall, of rows <paramref name="rowHeight"/> tall, as what a
    /// page moves; nothing for a view not laid out yet.</summary>
    public void TakeViewport(float viewport, float rowHeight)
    {
        if (viewport <= 0f)
        {
            return;
        }

        PageRows = (int)MathF.Max(1f, MathF.Floor(viewport / rowHeight) - 1f);
    }

    /// <summary>Records where the view was scrolled to as it was laid out.</summary>
    public void KeepScroll(float x, float y)
    {
        ScrollX = x;
        ScrollY = y;
    }
}

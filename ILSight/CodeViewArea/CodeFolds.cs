using System.Collections.Immutable;
using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>A code view's folds: which of a document's <see cref="DecompiledDocument.Folds"/> are collapsed, and
/// the rows that leaves: row r shows line <see cref="CodeFolding.GetLineAt(CodeFolds, CodeRow)"/>(r). Changed only through
/// its methods, which lay them out again when either changes, never per frame.</summary>
internal sealed class CodeFolds
{
    public CodeFoldLayout Layout { get; private set; } = CodeFoldLayout.None;

    public DecompiledDocument Document => Layout.Document;

    public ImmutableArray<bool> Collapsed => Layout.Collapsed;

    public ImmutableArray<int> Rows => Layout.Rows;

    public bool IsIdentity => Layout.IsIdentity;

    public int RowCount => Layout.RowCount;

    public bool AnyCollapsed => !IsIdentity;

    public void Reset(DecompiledDocument document)
    {
        var folds = document.Folds;
        var collapsed = new bool[folds.Length];
        for (var i = 0; i < folds.Length; i++)
        {
            collapsed[i] = folds[i].DefaultCollapsed;
        }

        Layout = CreateLayout(document, collapsed);
    }

    public void SetCollapsed(int fold, bool collapsed)
    {
        if (Collapsed[fold] == collapsed)
        {
            return;
        }

        var changed = Collapsed.ToArray();
        changed[fold] = collapsed;
        Layout = CreateLayout(Document, changed);
    }

    public void SetAll(bool collapsed)
    {
        var changed = new bool[Collapsed.Length];
        Array.Fill(changed, collapsed);
        Layout = CreateLayout(Document, changed);
    }

    /// <summary>Expands every fold that hides <paramref name="line"/>; returns whether any did.</summary>
    public bool TryReveal(int line)
    {
        if (IsIdentity)
        {
            return false;
        }

        var folds = Document.Folds;
        bool[]? opened = null;
        for (var i = 0; i < folds.Length && folds[i].StartLine < line; i++)
        {
            if (Collapsed[i] && line <= folds[i].EndLine)
            {
                opened ??= Collapsed.ToArray();
                opened[i] = false;
            }
        }
        if (opened is null)
        {
            return false;
        }

        Layout = CreateLayout(Document, opened);
        return true;
    }

    private static CodeFoldLayout CreateLayout(DecompiledDocument document, bool[] collapsed)
    {
        var lines = document.Lines.Length;
        var folds = document.Folds;
        var firstCollapsed = Array.IndexOf(collapsed, true);
        if (firstCollapsed < 0)
        {
            return new CodeFoldLayout { Document = document, Collapsed = [..collapsed], Rows = [], IsIdentity = true, RowCount = lines };
        }

        var rows = new int[lines];
        var count = 0;
        var next = 0;
        for (var line = 0; line < lines;)
        {
            rows[count++] = line;
            while (next < folds.Length && folds[next].StartLine < line)
            {
                next++;
            }

            line = next < folds.Length && folds[next].StartLine == line && collapsed[next] ? folds[next].EndLine + 1 : line + 1;
        }
        return new CodeFoldLayout { Document = document, Collapsed = [..collapsed], Rows = [..rows.AsSpan(0, count)], IsIdentity = false, RowCount = count };
    }
}

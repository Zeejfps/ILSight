using System.Collections.Immutable;
using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>A code view's folds: which of a document's <see cref="DecompiledDocument.Folds"/> are collapsed, and
/// the rows that leaves: row r shows line <see cref="CodeFolding.GetLineAt"/>(r). Changed only through
/// <see cref="CodeFolding"/>, which lays them out again when either changes, never per frame.</summary>
internal sealed class CodeFolds
{
    public CodeFoldLayout Layout = CodeFoldLayout.None;

    public DecompiledDocument Document => Layout.Document;

    public ImmutableArray<bool> Collapsed => Layout.Collapsed;

    public ImmutableArray<int> Rows => Layout.Rows;

    public bool IsIdentity => Layout.IsIdentity;

    public int RowCount => Layout.RowCount;

    public bool AnyCollapsed => !IsIdentity;
}

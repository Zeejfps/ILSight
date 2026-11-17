using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>What the screen names new things in English.</summary>
internal static class Names
{
    public static string Bookmark(int number)
    {
        return Strings.En.BookmarksDefaultName(number);
    }

    public static TabTitle Comparison(LoadedAssembly left)
    {
        return new TabTitle.Comparison(left.Name);
    }

    public static TabTitle Diagram(CodeReference type)
    {
        return new TabTitle.Diagram(type.Name);
    }

    /// <summary>The title <paramref name="saved"/> was kept under, in English.</summary>
    public static string? SavedTitle(DocumentRecord saved)
    {
        return saved.Content switch
        {
            NodeDocumentRecord node => node.Title,
            DiagramArea.DiagramDocumentRecord diagram => diagram.Title?.Words(Strings.En),
            ComparisonDocumentRecord comparison => comparison.Title?.Words(Strings.En),
        };
    }
}

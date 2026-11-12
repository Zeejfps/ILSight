using ILSight.Core;
using ILSight.Localization;

namespace ILSight.App.Tests;

/// <summary>What the screen names new things in English.</summary>
internal static class Names
{
    public static string Bookmark(int number)
    {
        return Strings.En.BookmarksDefaultName(number);
    }

    public static string Comparison(LoadedAssembly left)
    {
        return Strings.En.ComparisonTitle(left.Name);
    }

    public static string Diagram(CodeReference type)
    {
        return Strings.En.DiagramTitle(type.Name);
    }
}

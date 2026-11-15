using ILSight.Core;

namespace ILSight;

/// <summary>What each <see cref="DecompilationLanguage"/> is called in the interface: IL and C# by their names, which
/// no language translates; IL with C# in the language's words around them.</summary>
public static class DecompilationLanguageLabels
{
    private const string IL = "IL";
    private const string CSharp = "C#";

    /// <summary><paramref name="language"/>'s name in <paramref name="t"/>'s language.</summary>
    public static string Label(Strings t, DecompilationLanguage language)
    {
        return language switch
        {
            DecompilationLanguage.IL => IL,
            DecompilationLanguage.ILWithCSharp => t.ViewDecompilerIlWithCsharp(il: IL, csharp: CSharp),
            _ => CSharp,
        };
    }

    /// <summary><paramref name="language"/>'s name on the toolbar's switch in <paramref name="t"/>'s language: IL and
    /// C# by their names; IL with C# the two joined as the language's template joins them
    /// (<c>toolbar.il_with_csharp</c>, "IL+C#").</summary>
    public static string ShortLabel(Strings t, DecompilationLanguage language)
    {
        return language switch
        {
            DecompilationLanguage.IL => IL,
            DecompilationLanguage.ILWithCSharp => t.ToolbarIlWithCsharp(il: IL, csharp: CSharp),
            _ => CSharp,
        };
    }
}

namespace ILSight.Core;

/// <summary>What each <see cref="DecompilationLanguage"/> is called and what it shows.</summary>
public static class DecompilationLanguages
{
    public static IReadOnlyList<DecompilationLanguage> All { get; } = Enum.GetValues<DecompilationLanguage>();

    /// <summary>The language's name as ILSpy shows it.</summary>
    public static string Label(DecompilationLanguage language)
    {
        return language switch
        {
            DecompilationLanguage.IL => "IL",
            DecompilationLanguage.ILWithCSharp => "IL with C#",
            _ => "C#",
        };
    }

    /// <summary>Whether the language shows decompiled C#, which the decompiler options shape.</summary>
    public static bool ShowsCSharp(DecompilationLanguage language)
    {
        return language != DecompilationLanguage.IL;
    }

    /// <summary>Whether the language is disassembled to IL.</summary>
    public static bool IsIL(DecompilationLanguage language)
    {
        return language != DecompilationLanguage.CSharp;
    }
}

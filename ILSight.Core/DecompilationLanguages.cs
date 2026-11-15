namespace ILSight.Core;

/// <summary>What each <see cref="DecompilationLanguage"/> shows.</summary>
public static class DecompilationLanguages
{
    public static IReadOnlyList<DecompilationLanguage> All { get; } = Enum.GetValues<DecompilationLanguage>();

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

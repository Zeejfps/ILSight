namespace ILSight.Core;

/// <summary>The language a node is decompiled to.</summary>
public enum DecompilationLanguage
{
    CSharp,
    IL,

    /// <summary>IL with the C# each statement was decompiled from written above its first instruction.</summary>
    ILWithCSharp,
}

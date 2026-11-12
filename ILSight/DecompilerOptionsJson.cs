using ILSight.Core;

namespace ILSight;

/// <summary>The choices that change how C# is decompiled, as a settings file can hold them. What it
/// lacks, or holds as null, is as <see cref="DecompilerOptions.Default"/> has it.</summary>
internal sealed record DecompilerOptionsJson
{
    public CSharpVersion? LanguageVersion { get; init; }

    public Dictionary<string, bool>? Toggles { get; init; }
}

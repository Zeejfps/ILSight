using ILSight.Core;

namespace ILSight;

/// <summary>The language code is shown in and how C# is decompiled: the values the views only read and pass on
/// together, one value kept on <see cref="DecompilerChoice"/> and made again only when one of them changes, so a frame makes
/// none.</summary>
public sealed record DecompilerSetup
{
    public required DecompilationLanguage Language { get; init; }

    public required DecompilerOptions Options { get; init; }
}

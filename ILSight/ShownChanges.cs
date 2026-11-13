using ILSight.Core;
using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>What the screen last showed, to tell whether it is out of date. Each <c>TryTake*</c> records what it is
/// handed when it differs, so that what follows from a change happens once.</summary>
internal sealed class ShownChanges
{
    private CodeFont? _codeFont;
    private int _listGeneration;
    private (DecompilationLanguage Language, DecompilerOptions Options)? _decompilation;
    private (AppIcons Icons, IReadOnlyList<FontFace> Fonts)? _icons;

    /// <summary>The fonts of the icons the screen was last drawn with; none before the first.</summary>
    public IReadOnlyList<FontFace> IconFonts => _icons?.Fonts ?? [];

    /// <summary>A newly chosen font is registered once.</summary>
    public bool TryTakeCodeFont(CodeFont? font)
    {
        if (font == _codeFont)
        {
            return false;
        }

        _codeFont = font;
        return true;
    }

    public bool TryTakeListGeneration(int generation)
    {
        if (generation == _listGeneration)
        {
            return false;
        }

        _listGeneration = generation;
        return true;
    }

    /// <summary>By reference for the options: a record's equality would take new options with the same
    /// values as no change, and a tab would keep what was decompiled with the old ones.</summary>
    public bool TryTakeDecompilation(DecompilationLanguage language, DecompilerOptions options)
    {
        var shown = _decompilation;
        if (shown is not null && language == shown.Value.Language && ReferenceEquals(options, shown.Value.Options))
        {
            return false;
        }

        _decompilation = (language, options);
        return true;
    }

    /// <summary>By reference: reloaded icons are new ones, even with the same values.</summary>
    public bool TryTakeIcons(AppIcons icons, IReadOnlyList<FontFace> fonts)
    {
        var shown = _icons;
        if (shown is not null && ReferenceEquals(icons, shown.Value.Icons))
        {
            return false;
        }

        _icons = (icons, fonts);
        return true;
    }

    /// <summary>The families of <paramref name="before"/> that <paramref name="after"/> no longer draws in.</summary>
    public static List<string> ListDroppedFamilies(IReadOnlyList<FontFace> before, IReadOnlyList<FontFace> after)
    {
        var kept = after
            .Select(static f => f.Family)
            .ToHashSet();
        return before
            .Select(static f => f.Family)
            .Where(f => !kept.Contains(f))
            .Distinct()
            .ToList();
    }
}

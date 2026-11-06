using ILSight.Core;
using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>Whether what the screen last showed is out of date. Each records what it is handed when it
/// differs, so that what follows from a change happens once.</summary>
internal static class ShownChanges
{
    /// <summary>A newly chosen font is registered once.</summary>
    public static bool TakeCodeFont(UiState uiState, CodeFont? font)
    {
        if (font == uiState.ShownCodeFont)
            return false;
        uiState.ShownCodeFont = font;
        return true;
    }

    public static bool TakeListGeneration(UiState uiState, int generation)
    {
        if (generation == uiState.ShownListGeneration)
            return false;
        uiState.ShownListGeneration = generation;
        return true;
    }

    /// <summary>By reference for the options: a record's equality would take new options with the same
    /// values as no change, and a tab would keep what was decompiled with the old ones.</summary>
    public static bool TakeDecompilation(UiState uiState, DecompilationLanguage language, DecompilerOptions options)
    {
        var shown = uiState.ShownDecompilation;
        if (shown != null && language == shown.Value.Language && ReferenceEquals(options, shown.Value.Options))
            return false;
        uiState.ShownDecompilation = (language, options);
        return true;
    }

    /// <summary>By reference: reloaded icons are new ones, even with the same values.</summary>
    public static bool TakeIcons(UiState uiState, AppIcons icons, IReadOnlyList<FontFace> fonts)
    {
        var shown = uiState.ShownIcons;
        if (shown != null && ReferenceEquals(icons, shown.Value.Icons))
            return false;
        uiState.ShownIcons = (icons, fonts);
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

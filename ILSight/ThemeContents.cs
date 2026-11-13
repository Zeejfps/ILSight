using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>What a readable theme file sets: its palette, and its <c>ui</c>, <c>code</c> and <c>icons</c> colors by key.</summary>
public sealed record ThemeContents
{
    /// <summary>The named colors the other sections can use, by name.</summary>
    public required IReadOnlyDictionary<string, ThemeValue> Palette { get; init; }

    /// <summary>The colors of <see cref="UiTheme"/>, by key.</summary>
    public required IReadOnlyDictionary<string, ThemeValue> Ui { get; init; }

    /// <summary>The colors of <see cref="CodeViewArea.CodeViewPalette"/>, by key.</summary>
    public required IReadOnlyDictionary<string, ThemeValue> Code { get; init; }

    /// <summary>The colors of <see cref="NodeIconPalette"/>, by key.</summary>
    public required IReadOnlyDictionary<string, ThemeValue> Icons { get; init; }
}

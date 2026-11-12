using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>What a readable theme file sets: its palette, and its <c>ui</c>, <c>code</c> and <c>icons</c> colors by key.</summary>
/// <param name="Palette">The named colors the other sections can use, by name.</param>
/// <param name="Ui">The colors of <see cref="UiTheme"/>, by key.</param>
/// <param name="Code">The colors of <see cref="CodeViewPalette"/>, by key.</param>
/// <param name="Icons">The colors of <see cref="NodeIconPalette"/>, by key.</param>
public sealed record ThemeContents(
    IReadOnlyDictionary<string, ThemeValue> Palette,
    IReadOnlyDictionary<string, ThemeValue> Ui,
    IReadOnlyDictionary<string, ThemeValue> Code,
    IReadOnlyDictionary<string, ThemeValue> Icons);

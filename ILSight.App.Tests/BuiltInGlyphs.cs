using ZGF.Gui.Immediate;
using ILSight.BuiltInAssets;

namespace ILSight.App.Tests;

/// <summary>The icons of the built-in reference icon theme, which the tests' app draws with.</summary>
internal static class BuiltInGlyphs
{
    public static AppIcons Icons { get; } = BuiltInIconThemes.Reference.Icons;

    /// <summary>The text <paramref name="glyph"/>, a font glyph, is drawn as.</summary>
    public static string Text(IconGlyph glyph)
    {
        var text = glyph.Text;
        Assert.NotNull(text);
        return text;
    }
}

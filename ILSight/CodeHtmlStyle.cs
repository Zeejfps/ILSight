namespace ILSight;

/// <summary>How copied code looks as HTML: the code view's colors and the font it draws code in, in
/// pixels.</summary>
internal readonly record struct CodeHtmlStyle(
    CodeViewPalette Palette, uint Background, uint ErrorText, string FontFamily, float FontSize, float LineHeight);

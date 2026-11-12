namespace ILSight.CodeViewArea;

/// <summary>How copied code looks as HTML: the code view's colors and the font it draws code in, in
/// pixels.</summary>
internal readonly record struct CodeHtmlStyle
{
    public required CodeViewPalette Palette { get; init; }

    public required uint Background { get; init; }

    public required uint ErrorText { get; init; }

    public required string FontFamily { get; init; }

    public required float FontSize { get; init; }

    public required float LineHeight { get; init; }
}

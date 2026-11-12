using ZGF.Gui;
using ZGF.Gui.Testing;

namespace ILSight.App.Tests;

/// <summary>As wide as the app's font draws Latin text, which the default 8px a character overstates:
/// the test of what fits has to go by what the window would show.</summary>
internal sealed class AppFontWidths : ITextMeasurer
{
    private const float CharacterWidth = 6.5f;
    private const float LineHeight = 16f;

    public float MeasureTextWidth(ReadOnlySpan<char> text, TextStyle style)
    {
        return text.Length * CharacterWidth;
    }

    public float MeasureTextPrefix(ReadOnlySpan<char> text, int prefixLength, TextStyle style)
    {
        return Math.Clamp(prefixLength, 0, text.Length) * CharacterWidth;
    }

    public float MeasureTextLineHeight(TextStyle style)
    {
        return LineHeight;
    }
}

using ZGF.Gui;
using ZGF.Gui.Testing;

namespace ILSight.App.Tests;

/// <summary>Measures a digit five times as wide as any other character, so that a column of numbers is as wide as its
/// longest number, wider than its heading.</summary>
internal sealed class WideDigits : ITextMeasurer
{
    private const float CharacterWidth = 8f;
    private const float DigitWidth = 40f;
    private const float LineHeight = 16f;

    public float MeasureTextWidth(ReadOnlySpan<char> text, TextStyle style)
    {
        return Measure(text, text.Length);
    }

    public float MeasureTextPrefix(ReadOnlySpan<char> text, int prefixLength, TextStyle style)
    {
        return Measure(text, prefixLength);
    }

    /// <summary>How wide <paramref name="text"/> is measured, in any style.</summary>
    public static float Measure(string text)
    {
        return Measure(text, text.Length);
    }

    private static float Measure(ReadOnlySpan<char> text, int prefixLength)
    {
        var width = 0f;
        var end = Math.Clamp(prefixLength, 0, text.Length);
        for (var i = 0; i < end; i++)
        {
            width += char.IsAsciiDigit(text[i]) ? DigitWidth : CharacterWidth;
        }

        return width;
    }

    public float MeasureTextLineHeight(TextStyle style)
    {
        return LineHeight;
    }
}

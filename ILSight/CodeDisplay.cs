using ZGF.Fonts;
using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>What the display options work out: the code font of a system font, the size code is drawn at,
/// a zoom and the offered interface scale nearest a scale. <see cref="ScreenDisplay"/> holds the options.</summary>
public static class CodeDisplay
{
    private const float FontSizeStep = 1f;

    /// <summary>The font of <paramref name="family"/>'s regular face, or null when it has none.</summary>
    public static CodeFont? CreateFont(SystemFontFamily family)
    {
        var regular = family.FindRegular();
        if (regular == null)
        {
            return null;
        }

        return new CodeFont { Family = family.Name, Path = regular.File.Path, FaceIndex = regular.File.FaceIndex };
    }

    /// <summary>The size code is drawn at in <paramref name="theme"/>.</summary>
    public static float GetFontSize(DisplayOptions display, UiTheme theme)
    {
        if (display.FontSize is null)
        {
            return theme.MonoFontSize;
        }

        return display.FontSize.Value.Points;
    }

    /// <summary><paramref name="display"/> with code drawn at <paramref name="size"/>, kept in range.</summary>
    public static DisplayOptions SetFontSize(DisplayOptions display, float size)
    {
        return display with { FontSize = CodeFontSize.Clamp(size) };
    }

    /// <summary>The font size <paramref name="steps"/> steps larger, or smaller when negative, than
    /// it is in <paramref name="theme"/>; the theme's own again once it gets back to that size.</summary>
    public static DisplayOptions Zoom(DisplayOptions display, UiTheme theme, int steps)
    {
        var size = GetFontSize(display, theme) + steps * FontSizeStep;
        var zoomed = CodeFontSize.Clamp(size);
        if (zoomed.Points == theme.MonoFontSize)
        {
            return display with { FontSize = null };
        }

        return display with { FontSize = zoomed };
    }

    public static bool CanZoomIn(DisplayOptions display, UiTheme theme)
    {
        return GetFontSize(display, theme) < CodeFontSize.Max;
    }

    public static bool CanZoomOut(DisplayOptions display, UiTheme theme)
    {
        return GetFontSize(display, theme) > CodeFontSize.Min;
    }

    /// <summary><paramref name="display"/> with the offered interface scale nearest <paramref name="scale"/>.</summary>
    public static DisplayOptions SnapUiScale(DisplayOptions display, float scale)
    {
        var nearest = GetNearestUiScale(scale);
        return display with { UiScale = nearest };
    }

    private static float GetNearestUiScale(float scale)
    {
        return DisplayOptions.UiScales
            .MinBy(s => MathF.Abs(s - scale));
    }

    /// <summary>The name <paramref name="font"/> is offered by: the bundled font's when it is null.</summary>
    public static string GetFontName(CodeFont? font)
    {
        if (font is null)
        {
            return CodeFontChoices.BundledName;
        }

        return font.Family;
    }
}

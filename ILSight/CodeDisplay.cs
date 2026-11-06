using ZGF.Fonts;
using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>Changing how the app is drawn: the scale of the whole interface, and the font and size of
/// code and the aids drawn around it. The settings keep it with the rest of the screen.</summary>
public static class CodeDisplay
{
    private const float FontSizeStep = 1f;

    /// <summary>The font of <paramref name="family"/>'s regular face, or null when it has none.</summary>
    public static CodeFont? CreateFont(SystemFontFamily family)
    {
        var regular = family.FindRegular();
        if (regular == null)
            return null;
        return new CodeFont(family.Name, regular.File.Path, regular.File.FaceIndex);
    }

    /// <summary>The size code is drawn at in <paramref name="theme"/>.</summary>
    public static float GetFontSize(DisplayOptions display, UiTheme theme)
    {
        if (display.FontSize == null)
            return theme.MonoFontSize;
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
            return display with { FontSize = null };
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
        var nearest = FindNearestUiScale(scale);
        return display with { UiScale = nearest };
    }

    private static float FindNearestUiScale(float scale)
    {
        return DisplayOptions.UiScales
            .MinBy(s => MathF.Abs(s - scale));
    }

    public static void SetDisplayOptions(UiState uiState, DisplayOptions options)
    {
        uiState.Display = options;
    }

    /// <summary>Draws code in <paramref name="font"/>, or in the bundled font when it is null.</summary>
    public static void SetCodeFont(UiState uiState, CodeFont? font)
    {
        var chosen = uiState.Display with { Font = font };
        SetDisplayOptions(uiState, chosen);
    }

    /// <summary>Writes each type's and member's metadata token in the tree as <paramref name="format"/> says.</summary>
    public static void SetTreeTokens(UiState uiState, TokenFormat format)
    {
        var chosen = uiState.Display with { TreeTokens = format };
        SetDisplayOptions(uiState, chosen);
    }

    /// <summary>Offers the installed fonts of <paramref name="families"/> that code can be drawn in.</summary>
    public static void KeepInstalledFonts(UiState uiState, IReadOnlyList<SystemFontFamily> families)
    {
        uiState.CodeFonts.Keep(families);
    }

    /// <summary>Draws the whole interface at <paramref name="scale"/>, or the offered scale nearest it.</summary>
    public static void SetUiScale(UiState uiState, float scale)
    {
        var scaled = SnapUiScale(uiState.Display, scale);
        SetDisplayOptions(uiState, scaled);
    }

    /// <summary>Makes code <paramref name="steps"/> font sizes larger, or smaller when negative.</summary>
    public static void ZoomCode(UiState uiState, int steps)
    {
        var zoomed = Zoom(uiState.Display, uiState.Theme, steps);
        SetDisplayOptions(uiState, zoomed);
    }

    /// <summary>Draws code at the theme's size again.</summary>
    public static void ResetCodeZoom(UiState uiState)
    {
        var reset = uiState.Display with { FontSize = null };
        SetDisplayOptions(uiState, reset);
    }

    /// <summary>Asks the user whether to reset the display options before <see cref="ResetDisplayOptions"/> does.</summary>
    public static void AskToResetDisplayOptions(UiState uiState)
    {
        uiState.ConfirmingDisplayReset = true;
    }

    /// <summary>Stops asking, resetting the display options when <paramref name="confirmed"/>.</summary>
    public static void FinishDisplayOptionsReset(UiState uiState, bool confirmed)
    {
        uiState.ConfirmingDisplayReset = false;
        if (confirmed)
            ResetDisplayOptions(uiState);
    }

    public static void ResetDisplayOptions(UiState uiState)
    {
        SetDisplayOptions(uiState, DisplayOptions.Default);
    }
}

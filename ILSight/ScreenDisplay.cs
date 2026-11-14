using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>How the screen draws: the display options the user chose (the scale of the whole interface, the
/// font and size of code and the aids drawn around it), and whether to reset them, asked of the user. The
/// settings keep the options with the rest of the screen.</summary>
public sealed class ScreenDisplay
{
    private DisplayOptions _options = DisplayOptions.Default;

    public required DisplayOptions Options
    {
        get => _options;
        init => _options = value;
    }

    /// <summary>Whether to put every display option back to its default, asked of the user.</summary>
    public Confirmation Reset { get; } = new();

    public void Set(DisplayOptions options)
    {
        _options = options;
    }

    /// <summary>Draws code in <paramref name="font"/>, or in the bundled font when it is null.</summary>
    public void SetCodeFont(CodeFont? font)
    {
        _options = _options with { Font = font };
    }

    /// <summary>Writes each type's and member's metadata token in the tree as <paramref name="format"/> says.</summary>
    public void SetTreeTokens(TokenFormat format)
    {
        _options = _options with { TreeTokens = format };
    }

    /// <summary>Draws the whole interface at <paramref name="scale"/>, or the offered scale nearest it.</summary>
    public void SetUiScale(float scale)
    {
        _options = CodeDisplay.SnapUiScale(_options, scale);
    }

    /// <summary>Makes code <paramref name="steps"/> font sizes larger, or smaller when negative, than it is
    /// in <paramref name="theme"/>.</summary>
    public void ZoomCode(UiTheme theme, int steps)
    {
        _options = CodeDisplay.Zoom(_options, theme, steps);
    }

    /// <summary>Draws code at the theme's size again.</summary>
    public void ResetCodeZoom()
    {
        _options = _options with { FontSize = null };
    }

    /// <summary>Stops asking, resetting the display options when <paramref name="confirmed"/>.</summary>
    public void FinishReset(bool confirmed)
    {
        Reset.Stop();
        if (confirmed)
        {
            _options = DisplayOptions.Default;
        }
    }
}

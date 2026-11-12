using ZGF.Gui.Immediate;
using ILSight.CodeViewArea;

namespace ILSight;

/// <summary>Everything the app is drawn with in one theme: the UI theme, the editor's colors and
/// the tree's icon colors.</summary>
public sealed record AppTheme
{
    public required UiTheme Ui { get; init; }

    public required CodeViewPalette Code { get; init; }

    public required NodeIconPalette Icons { get; init; }
}

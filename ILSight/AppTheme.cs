using ZGF.Gui.Immediate;
using ILSight.CodeViewArea;

namespace ILSight;

/// <summary>Everything the app is drawn with in one theme: the UI theme, the editor's colors and
/// the tree's icon colors.</summary>
public sealed record AppTheme(UiTheme Ui, CodeViewPalette Code, NodeIconPalette Icons);

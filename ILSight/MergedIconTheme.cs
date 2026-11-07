using System.Diagnostics;
using System.Globalization;
using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>An icon theme's fonts and icons merged over those of the themes it extends, before fonts are
/// looked up. Built once at the end, so an icon a parent draws in a font takes the font a child declared
/// under that id, and an icon whose font is unknown or broken keeps the one beneath it.</summary>
internal sealed class MergedIconTheme
{
    public readonly Dictionary<string, IconFont> Fonts = new();
    public readonly Dictionary<string, List<IconValue>> Icons = new();
}

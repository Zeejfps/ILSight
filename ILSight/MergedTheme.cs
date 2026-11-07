using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>A theme's palette and colors merged over those of the themes it extends, before palette names
/// are looked up. Built once at the end, so a color a parent names takes the palette color a child set, a
/// color whose palette name is unknown keeps the one beneath it, and a role nobody set is derived from the
/// final base roles.</summary>
internal sealed class MergedTheme
{
    public readonly Dictionary<string, ThemeValue> Palette = new();
    public readonly Dictionary<string, List<ThemeValue>> Ui = new();
    public readonly Dictionary<string, List<ThemeValue>> Code = new();
    public readonly Dictionary<string, List<ThemeValue>> Icons = new();
}

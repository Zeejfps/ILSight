using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>The colors node icons are drawn in, one per <see cref="NodeIconRole"/>: muted hues
/// shared with the editor's syntax colors, so color supplements the pictogram rather than
/// carrying meaning alone.</summary>
[ThemeColors]
public sealed partial class NodeIconPalette
{
    public required uint Neutral { get; init; }
    public required uint Type { get; init; }
    public required uint Method { get; init; }
    public required uint Data { get; init; }
    public required uint Error { get; init; }

    /// <summary>The color of the accessibility overlay on non-public nodes' icons.</summary>
    public required uint Restricted { get; init; }

    public uint this[NodeIconRole role] => role switch
    {
        NodeIconRole.Type => Type,
        NodeIconRole.Method => Method,
        NodeIconRole.Data => Data,
        NodeIconRole.Error => Error,
        _ => Neutral,
    };
}

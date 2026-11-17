using ZGF.Gui.Immediate.Widgets;

namespace ILSight.SettingArea;

/// <summary>What a dock layout a settings file holds reads as.</summary>
internal union DockLayoutRead(DockLayoutRead.Parsed, DockLayoutRead.PartsMissing, DockLayoutRead.IdsRepeated)
{
    /// <summary>The layout, with <see cref="DockLayout.NextId"/> above every id it uses.</summary>
    public sealed record Parsed
    {
        public required DockLayout Layout { get; init; }
    }

    /// <summary>A part of the layout is held as null.</summary>
    public sealed record PartsMissing;

    /// <summary>Two of its nodes or windows have the same id, so they would move together.</summary>
    public sealed record IdsRepeated;
}

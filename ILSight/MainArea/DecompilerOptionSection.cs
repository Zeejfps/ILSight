using ILSight.Core;

namespace ILSight.MainArea;

/// <summary>A group of the decompiler's toggles as the options panel shows it: its key, the group and its
/// toggles.</summary>
internal sealed record DecompilerOptionSection
{
    public required string Key { get; init; }

    public required DecompilerToggleGroup Group { get; init; }

    public required DecompilerToggle[] Toggles { get; init; }
}

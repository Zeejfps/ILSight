using ILSight.Core;

namespace ILSight;

/// <summary>A group of the decompiler's toggles as the options panel shows it: its key, the group and its
/// toggles.</summary>
internal sealed record DecompilerOptionSection(string Key, DecompilerToggleGroup Group, DecompilerToggle[] Toggles);

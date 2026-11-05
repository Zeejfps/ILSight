using ILSight.Core;

namespace ILSight;

/// <summary>What a command asks to open and show: its files, and the documentation id of what to
/// show in them, or null to show the last file opened.</summary>
public sealed record FilesToOpen(List<AssemblyPath> Paths, string? NavigateTo);

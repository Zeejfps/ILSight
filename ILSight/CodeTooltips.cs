using ZGF.Gui.Immediate;
using ILSight.Core;

namespace ILSight;

/// <summary>
/// The tooltips of references hovered in code: each is looked up once, in the background, and kept
/// while the open assemblies stay the same. A lookup that finishes after they changed is dropped.
/// </summary>
public sealed class CodeTooltips
{
    public required AssemblyList Assemblies { get; init; }
    /// <summary>Where the work it starts in the background is counted until it ends.</summary>
    public required BackgroundWork Work { get; init; }

    public required Action<AppProblem> ReportError { get; init; }
    public readonly Dictionary<CodeReference.Entity, CodeTooltip?> Found = new();
    public readonly HashSet<CodeReference.Entity> Pending = new();
    public required int Version;

    /// <summary>Whether a lookup is still running in the background.</summary>
    public bool IsLookingUp => Pending.Count > 0;
}

using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>Something an export that finished could not do, for the screen to tell in the chosen language.</summary>
public abstract record ExportProblem
{
    private ExportProblem()
    {
    }

    /// <summary>Part of a project could not be decompiled.</summary>
    public sealed record NotDecompiled(ExportError Error) : ExportProblem;

    /// <summary>The assembly <see cref="Assembly"/> has no entry point a PDB can be written for.</summary>
    public sealed record NoPdbEntry(string Assembly) : ExportProblem;

    public sealed record PdbNotWritten(string Assembly, ExportError Error) : ExportProblem;

    /// <summary>The package file at <see cref="Path"/> was skipped: its path leads outside the folder.</summary>
    public sealed record OutsideFolder(string Path) : ExportProblem;

    public sealed record FileNotWritten(string Path, ExportError Error) : ExportProblem;
}

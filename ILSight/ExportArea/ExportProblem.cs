using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>Something an export that finished could not do, for the screen to tell in the chosen language.</summary>
public union ExportProblem(ExportProblem.NotDecompiled, ExportProblem.NoPdbEntry, ExportProblem.PdbNotWritten,
    ExportProblem.OutsideFolder, ExportProblem.FileNotWritten)
{
    /// <summary>Part of a project could not be decompiled.</summary>
    public sealed record NotDecompiled(ExportError Error);

    /// <summary>The assembly <see cref="Assembly"/> has no entry point a PDB can be written for.</summary>
    public sealed record NoPdbEntry(string Assembly);

    public sealed record PdbNotWritten
    {
        public required string Assembly { get; init; }

        public required ExportError Error { get; init; }
    }

    /// <summary>The package file at <see cref="Path"/> was skipped: its path leads outside the folder.</summary>
    public sealed record OutsideFolder(string Path);

    public sealed record FileNotWritten
    {
        public required string Path { get; init; }

        public required ExportError Error { get; init; }
    }
}

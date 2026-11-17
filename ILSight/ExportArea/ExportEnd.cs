namespace ILSight.ExportArea;

/// <summary>How an export ended. One that <see cref="Finished"/> may still have problems: parts it
/// could not decompile, or PDBs it could not write. One refused because its assembly was closed
/// (<see cref="AssemblyClosed"/>) is neither cancelled nor failed.</summary>
public union ExportEnd(ExportEnd.Finished, ExportEnd.Cancelled, ExportEnd.AssemblyClosed, ExportEnd.Failed)
{
    /// <summary>The export ran to its end, writing <see cref="Written"/>, with <see cref="Problems"/> in
    /// what it could not do.</summary>
    public sealed record Finished
    {
        public required IReadOnlyList<string> Written { get; init; }

        public required IReadOnlyList<ExportProblem> Problems { get; init; }

        public bool HasProblems => Problems.Count > 0;
    }

    /// <summary>The export was cancelled before it finished.</summary>
    public sealed record Cancelled;

    /// <summary>The export was refused, nothing written: <see cref="Assembly"/> (a file name), the assembly it reads,
    /// was closed before its work read it (while its dialog was open, or while it waited its turn). Not a cancel, which
    /// the user asks for, nor a failure, so never reported as an error.</summary>
    public sealed record AssemblyClosed
    {
        public required string Assembly { get; init; }
    }

    /// <summary>The export failed as a whole with <see cref="Error"/>; <see cref="Subject"/> names what it
    /// was exporting, empty when the export's kind says it.</summary>
    public sealed record Failed
    {
        public required string Subject { get; init; }

        public required Exception Error { get; init; }
    }
}

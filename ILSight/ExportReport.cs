using System.Diagnostics;
using ILSight.Core;

namespace ILSight;

/// <summary>How an export ended. One that <see cref="Finished"/> may still have problems: parts it
/// could not decompile, or PDBs it could not write.</summary>
public abstract record ExportEnd
{
    private ExportEnd()
    {
    }

    /// <summary>The export ran to its end, writing <see cref="Written"/>, with <see cref="Problems"/> in
    /// what it could not do.</summary>
    public sealed record Finished : ExportEnd
    {
        public required IReadOnlyList<string> Written { get; init; }

        public required IReadOnlyList<ExportProblem> Problems { get; init; }

        public bool HasProblems => Problems.Count > 0;
    }

    /// <summary>The export was cancelled before it finished.</summary>
    public sealed record Cancelled : ExportEnd;

    /// <summary>The export failed as a whole with <see cref="Error"/>; <see cref="Subject"/> names what it
    /// was exporting, empty when the export's kind says it.</summary>
    public sealed record Failed : ExportEnd
    {
        public required string Subject { get; init; }

        public required Exception Error { get; init; }
    }

    public TResult Match<TResult>(Func<Finished, TResult> finished, Func<Cancelled, TResult> cancelled, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Finished f => finished(f),
            Cancelled c => cancelled(c),
            Failed f => failed(f),
            _ => throw new UnreachableException($"ExportEnd.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary><see cref="Match{TResult}"/> handing <paramref name="state"/> to the handlers, so that a
    /// caller drawing every frame can pass static lambdas and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Finished, TResult> finished, Func<TState, Cancelled, TResult> cancelled,
        Func<TState, Failed, TResult> failed)
    {
        return this switch
        {
            Finished f => finished(state, f),
            Cancelled c => cancelled(state, c),
            Failed f => failed(state, f),
            _ => throw new UnreachableException($"ExportEnd.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Runs the handler for this case with <paramref name="state"/>, for effects that differ per
    /// case; static lambdas allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Finished> finished, Action<TState, Cancelled> cancelled, Action<TState, Failed> failed)
    {
        switch (this)
        {
            case Finished f:
                finished(state, f);
                break;
            case Cancelled c:
                cancelled(state, c);
                break;
            case Failed f:
                failed(state, f);
                break;
            default:
                throw new UnreachableException($"ExportEnd.Switch does not handle {GetType().Name}.");
        }
    }
}

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

/// <summary>What one export did: which it was, how it ended, with the files it wrote and what went wrong,
/// how long it took, and whether its folder was there when it ended.</summary>
public sealed record ExportReport(
    ExportRun Run,
    ExportJob Job,
    ExportEnd End,
    TimeSpan Elapsed,
    bool FolderExists)
{
    public string Name => Job.Name;

    public string Folder => Job.Folder;
}

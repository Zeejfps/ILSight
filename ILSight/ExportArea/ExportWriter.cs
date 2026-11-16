using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>Runs an export the exports panel queued (<see cref="Exports.Enter"/>) once its turn comes: writes it
/// through <see cref="IPorts"/> without holding up the screen, ends its turn, and adds the report of how it ended to the
/// finished exports: what it wrote and what went wrong, that it was cancelled, that it was refused because its assembly
/// was closed, or why it failed. One taken out of the
/// queue before its turn leaves nothing behind.</summary>
public sealed class ExportWriter(ExportWriter.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>A reading of a clock that only goes forward, for <see cref="MeasureSince"/>.</summary>
        ClockReading Now { get; }

        /// <summary>How long it has been since <paramref name="start"/>, a reading of <see cref="Now"/>.</summary>
        TimeSpan MeasureSince(ClockReading start);

        bool IsFolderThere(string folder);

        /// <summary>Writes the code <paramref name="work"/> asks for; answers the path written. Each of these writes
        /// without holding up the screen, reporting how far it has got to <paramref name="start"/>; answers
        /// <see cref="ExportOutcome{T}.Stopped"/> once <paramref name="start"/>'s turn is cancelled,
        /// <see cref="ExportOutcome{T}.Closed"/> when the assembly it reads was closed before it read it, and what the
        /// work failed with as <see cref="ExportOutcome{T}.Failed"/>; the cancellation of <paramref name="ct"/> is let
        /// through.</summary>
        Task<ExportOutcome<string>> WriteCodeFileAsync(ExportWork.CodeFile work, ExportTurn.Started start, CancellationToken ct);

        Task<ExportOutcome<ExportedProject>> WriteProjectAsync(ExportWork.Project work, ExportTurn.Started start, CancellationToken ct);

        Task<ExportOutcome<List<ExportedProject>>> WriteSolutionAsync(ExportWork.Solution work, ExportTurn.Started start, CancellationToken ct);

        /// <summary>Answers the path written.</summary>
        Task<ExportOutcome<string>> WritePdbAsync(ExportWork.Pdb work, ExportTurn.Started start, CancellationToken ct);

        /// <summary>Answers the assemblies whose PDB could not be written, and why.</summary>
        Task<ExportOutcome<List<PdbFailure>>> WritePdbsAsync(ExportWork.Pdbs work, ExportTurn.Started start, CancellationToken ct);

        /// <summary>Answers the diagrammer's page.</summary>
        Task<ExportOutcome<string>> WriteDiagramAsync(ExportWork.Diagram work, ExportTurn.Started start, CancellationToken ct);

        Task<ExportOutcome<ExtractedFiles>> ExtractPackageFolderAsync(ExportWork.PackageFolder work, ExportTurn.Started start, CancellationToken ct);
    }

    /// <summary>Waits for the turn of the export <paramref name="ticket"/> queued, then writes <paramref name="work"/>
    /// and adds its report to <paramref name="finished"/>; a diagram that was written is opened through
    /// <paramref name="viewCommands"/>. Nothing when it was withdrawn. <paramref name="ct"/> is the workflow's token;
    /// cancelling the export's turn stops its work and is reported as cancelled; an assembly closed before the work read
    /// it is reported as refused, saying so, never as cancelled nor as an error.</summary>
    public async Task HandleAsync(List<ExportReport> finished, Queue<ViewCommand> viewCommands, ExportWork work, ExportTicket ticket,
        CancellationToken ct)
    {
        var turn = await ticket.Turn.WaitAsync(ct);
        if (turn is not ExportTurn.Started start)
        {
            return;
        }

        var started = ports.Now;
        var subject = work.Subject;
        var report = await work.Match(
            code => WriteAsync(token => ports.WriteCodeFileAsync(code, start, token), start, ticket, subject, started,
                static (r, written) => Exports.ReportWritten(r, written), ct),
            project => WriteAsync(token => ports.WriteProjectAsync(project, start, token), start, ticket, subject, started,
                (r, exported) => Exports.ReportProjects(r, [exported], project.ProjectFile), ct),
            solution => WriteAsync(token => ports.WriteSolutionAsync(solution, start, token), start, ticket, subject, started,
                (r, exported) => Exports.ReportProjects(r, exported, solution.SolutionFile), ct),
            pdb => WriteAsync(token => ports.WritePdbAsync(pdb, start, token), start, ticket, subject, started,
                static (r, written) => Exports.ReportWritten(r, written), ct),
            pdbs => WriteAsync(token => ports.WritePdbsAsync(pdbs, start, token), start, ticket, subject, started,
                (r, failures) => Exports.ReportPdbs(r, pdbs.Assemblies, failures, pdbs.Folder), ct),
            diagram => WriteAsync(token => ports.WriteDiagramAsync(diagram, start, token), start, ticket, subject, started,
                (r, page) => Exports.OpenDiagram(viewCommands, r, page), ct),
            folder => WriteAsync(token => ports.ExtractPackageFolderAsync(folder, start, token), start, ticket, subject, started,
                static (r, extracted) => Exports.ReportExtracted(r, extracted), ct));
        finished.Add(report);
    }

    private async Task<ExportReport> WriteAsync<T>(Func<CancellationToken, Task<ExportOutcome<T>>> write, ExportTurn.Started start, ExportTicket ticket,
        string subject, ClockReading started, Func<ExportReport, T, ExportReport> complete, CancellationToken ct)
    {
        var outcome = await write(ct);
        start.EndTurn();
        var run = ticket.Run;
        var job = ticket.Job;
        var elapsed = ports.MeasureSince(started);
        var folderExists = ports.IsFolderThere(job.Folder);
        return outcome.Match(
            done => complete(new ExportReport
            {
                Run = run,
                Job = job,
                End = new ExportEnd.Finished { Written = [], Problems = [] },
                Elapsed = elapsed,
                FolderExists = folderExists,
            }, done.Value),
            _ => new ExportReport
            {
                Run = run,
                Job = job,
                End = new ExportEnd.Cancelled(),
                Elapsed = elapsed,
                FolderExists = folderExists,
            },
            closed => new ExportReport
            {
                Run = run,
                Job = job,
                End = new ExportEnd.AssemblyClosed { Assembly = closed.Assembly },
                Elapsed = elapsed,
                FolderExists = folderExists,
            },
            failed => new ExportReport
            {
                Run = run,
                Job = job,
                End = new ExportEnd.Failed { Subject = subject, Error = failed.Error },
                Elapsed = elapsed,
                FolderExists = folderExists,
            });
    }
}

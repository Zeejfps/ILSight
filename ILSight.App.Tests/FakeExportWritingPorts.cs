using ILSight.Core;
using ILSight.ExportArea;

namespace ILSight.App.Tests;

/// <summary>Writes nothing: answers code files and diagrams as the test says, finds every folder there or none, and
/// says two seconds have gone by whenever asked; keeps the work it was asked to write.</summary>
internal sealed class FakeExportWritingPorts : ExportWriting.IPorts
{
    public ExportOutcome<string> CodeFile { get; init; } = new ExportOutcome<string>.Done("written.cs");

    public ExportOutcome<string> Diagram { get; init; } = new ExportOutcome<string>.Done("index.html");

    public bool AreFoldersThere { get; init; } = true;

    public List<ExportWork> Asked { get; } = [];

    public long Now => 0;

    public TimeSpan MeasureSince(long start)
    {
        return TimeSpan.FromSeconds(2);
    }

    public bool IsFolderThere(string folder)
    {
        return AreFoldersThere;
    }

    public Task<ExportOutcome<string>> WriteCodeFileAsync(ExportWork.CodeFile work, ExportStart start, CancellationToken ct)
    {
        Asked.Add(work);
        return Task.FromResult(CodeFile);
    }

    public Task<ExportOutcome<ExportedProject>> WriteProjectAsync(ExportWork.Project work, ExportStart start, CancellationToken ct)
    {
        throw new NotSupportedException("No test writes a project through the fake.");
    }

    public Task<ExportOutcome<List<ExportedProject>>> WriteSolutionAsync(ExportWork.Solution work, ExportStart start, CancellationToken ct)
    {
        throw new NotSupportedException("No test writes a solution through the fake.");
    }

    public Task<ExportOutcome<string>> WritePdbAsync(ExportWork.Pdb work, ExportStart start, CancellationToken ct)
    {
        throw new NotSupportedException("No test writes a PDB through the fake.");
    }

    public Task<ExportOutcome<List<PdbFailure>>> WritePdbsAsync(ExportWork.Pdbs work, ExportStart start, CancellationToken ct)
    {
        throw new NotSupportedException("No test writes PDBs through the fake.");
    }

    public Task<ExportOutcome<string>> WriteDiagramAsync(ExportWork.Diagram work, ExportStart start, CancellationToken ct)
    {
        Asked.Add(work);
        return Task.FromResult(Diagram);
    }

    public Task<ExportOutcome<ExtractedFiles>> ExtractPackageFolderAsync(ExportWork.PackageFolder work, ExportStart start, CancellationToken ct)
    {
        throw new NotSupportedException("No test extracts a package folder through the fake.");
    }
}

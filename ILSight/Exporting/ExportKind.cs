namespace ILSight.Exporting;

/// <summary>What an export writes.</summary>
public enum ExportKind
{
    CSharpFile,
    ILFile,
    Project,
    Solution,
    Pdb,
    Pdbs,
    Files,
    Diagram,
}

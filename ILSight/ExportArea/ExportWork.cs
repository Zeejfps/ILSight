using System.Diagnostics;
using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>What one export the exports panel queues writes, one case per kind, with what it is written from: the
/// export as the panel shows it (<see cref="Job"/>) and what a failure says it was exporting (<see cref="Subject"/>)
/// follow from it.</summary>
public union ExportWork(ExportWork.CodeFile, ExportWork.Project, ExportWork.Solution, ExportWork.Pdb, ExportWork.Pdbs,
    ExportWork.Diagram, ExportWork.PackageFolder)
{
    /// <summary>The export as the user asked for it: what it is named after, what it writes, and its folder.</summary>
    public ExportJob Job => this switch
    {
        CodeFile c => new ExportJob { Name = c.Node.Text, Kind = CodeKindOf(c.Language), Folder = FolderOf(c.File) },
        Project p => new ExportJob
        {
            Name = Path.GetFileNameWithoutExtension(p.ProjectFile),
            Kind = ExportKind.Project,
            Folder = FolderOf(p.ProjectFile),
        },
        Solution s => new ExportJob
        {
            Name = Path.GetFileNameWithoutExtension(s.SolutionFile),
            Kind = ExportKind.Solution,
            Folder = FolderOf(s.SolutionFile),
        },
        Pdb p => new ExportJob { Name = p.Assembly.Name, Kind = ExportKind.Pdb, Folder = FolderOf(p.File) },
        Pdbs p => new ExportJob { Name = Path.GetFileName(p.Folder), Kind = ExportKind.Pdbs, Folder = p.Folder },
        Diagram d => new ExportJob { Name = d.Assembly.Name, Kind = ExportKind.Diagram, Folder = d.Folder },
        PackageFolder f => new ExportJob { Name = f.Name, Kind = ExportKind.Files, Folder = f.Folder },
    };

    /// <summary>What a failure says the export was exporting; empty when its kind says it.</summary>
    public string Subject => this switch
    {
        CodeFile c => c.Node.Text,
        Project p => p.Assembly.Name,
        Pdb p => p.Assembly.Name,
        Diagram d => d.Assembly.Name,
        PackageFolder f => f.Name,
        Solution or Pdbs => "",
    };

    /// <summary>The code of <see cref="Node"/> in <see cref="Language"/>, as one file at <see cref="File"/>, which a
    /// save dialog gave, the app's notes beside the code in <see cref="Notes"/>' words.</summary>
    public sealed record CodeFile
    {
        public required BrowseNode Node { get; init; }

        public required DecompilationLanguage Language { get; init; }

        public required DecompilerOptions Options { get; init; }

        public required IDocumentNotes Notes { get; init; }

        public required string File { get; init; }
    }

    /// <summary><see cref="Assembly"/> decompiled into a C# project whose file is <see cref="ProjectFile"/>, with what
    /// <see cref="Extras"/> asks for besides.</summary>
    public sealed record Project
    {
        public required LoadedModule Assembly { get; init; }

        public required DecompilerOptions Options { get; init; }

        public required string ProjectFile { get; init; }

        public required ProjectExtras Extras { get; init; }
    }

    /// <summary>Each of <see cref="Projects"/> decompiled into a C# project in a folder of its own beside the solution
    /// at <see cref="SolutionFile"/>.</summary>
    public sealed record Solution
    {
        public required IReadOnlyList<SolutionProject> Projects { get; init; }

        public required DecompilerOptions Options { get; init; }

        public required string SolutionFile { get; init; }

        public required ProjectExtras Extras { get; init; }
    }

    /// <summary>A portable PDB of <see cref="Assembly"/>'s decompiled C#, written to <see cref="File"/>.</summary>
    public sealed record Pdb
    {
        public required LoadedModule Assembly { get; init; }

        public required DecompilerOptions Options { get; init; }

        public required string File { get; init; }
    }

    /// <summary>A portable PDB of each of <see cref="Assemblies"/>, written into <see cref="Folder"/> named after the
    /// assembly.</summary>
    public sealed record Pdbs
    {
        public required IReadOnlyList<LoadedModule> Assemblies { get; init; }

        public required DecompilerOptions Options { get; init; }

        public required string Folder { get; init; }
    }

    /// <summary>The HTML class diagrammer of <see cref="Assembly"/>, written into <see cref="Folder"/>, a folder of its
    /// own named after it, and opened once written.</summary>
    public sealed record Diagram
    {
        public required LoadedModule Assembly { get; init; }

        public required string Folder { get; init; }
    }

    /// <summary>Every file in the folder of a package <see cref="Place"/> stands for, shown as <see cref="Name"/>,
    /// written into <see cref="Folder"/> at its path from that folder.</summary>
    public sealed record PackageFolder
    {
        public required string Name { get; init; }

        public required PackageFolderPlace Place { get; init; }

        public required string Folder { get; init; }
    }

    private static ExportKind CodeKindOf(DecompilationLanguage language)
    {
        if (language == DecompilationLanguage.CSharp)
        {
            return ExportKind.CSharpFile;
        }

        return ExportKind.ILFile;
    }

    private static string FolderOf(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (folder is null)
        {
            throw new UnreachableException($"'{path}', which a save dialog gave, names no file in a folder.");
        }

        return folder;
    }
}

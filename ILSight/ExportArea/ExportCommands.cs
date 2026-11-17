using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>The edge where the exports the screen asks for start, once the frame is drawn: each is queued and its
/// writing started as a workflow (<see cref="ExportWriter"/>), which waits for its turn; a resource or a single file of
/// a package starts writing at once, as a workflow of its own, counted in the app's background work until it
/// ends.</summary>
[Edge]
public static class ExportCommands
{
    /// <summary>Saves <paramref name="node"/> to the file at <paramref name="path"/>: a resource's bytes as they are,
    /// else its code as one file (<see cref="Exports.ToCodeSave"/>). Projects come from the export dialog.</summary>
    /// <remarks>The code is in <paramref name="setup"/>'s language and with its options as they were when the user
    /// asked for the save, the setup the file was named for (<see cref="SaveDialogs.PickCodeFile"/>): the code the
    /// user was looking at. A language or option changed while the save dialog was open reaches neither; the current
    /// setup is never read here. The dialog's title tells the user so ("Save C# code as shown when you asked"). The notes the app writes into the file beside the code (a reference read by name
    /// only, why the code could not be decompiled, a summary's labels) are in the app's language as it stands when the
    /// save runs, once the file is picked, as the tab shows them; the code itself is in no app language.</remarks>
    public static void SaveCode(AppState appState, BrowseNode node, DecompilerSetup setup, string path)
    {
        var save = Exports.ToCodeSave(node, setup, DocumentNoteTexts.For(appState.Locale), path);
        switch (save)
        {
            case CodeSave.ResourceBytes resource:
                SaveResource(appState, resource.Node, resource.File);
                break;
            case CodeSave.Code code:
                StartWriting(appState, code.Work);
                break;
        }
    }

    /// <summary>Starts writing the bytes of the resource <paramref name="node"/> stands for to the file at
    /// <paramref name="path"/> (<see cref="SaveResourceHandler"/>), off the frame thread, on a token the app's shutdown
    /// cancels.</summary>
    public static void SaveResource(AppState appState, BrowseNode node, string path)
    {
        appState.Workflows.Start(nameof(SaveResourceHandler), ct => appState.SaveResourceHandler.HandleAsync(node, path, ct));
    }

    /// <summary>Starts writing the file of a package <paramref name="place"/> stands for, as the package holds it, to
    /// the file at <paramref name="path"/> (<see cref="ExtractPackageFileHandler"/>), off the frame thread, on a token
    /// the app's shutdown cancels.</summary>
    public static void ExtractPackageFile(AppState appState, PackagePlace place, string path)
    {
        appState.Workflows.Start(nameof(ExtractPackageFileHandler), ct => appState.ExtractPackageFileHandler.HandleAsync(place, path, ct));
    }

    /// <summary>Exports what <paramref name="request"/> asks for: one assembly as a project straight into the folder,
    /// several as a solution with a folder per project.</summary>
    /// <remarks>The decompiler's options are taken as they stand when the request starts (the dialog's own options
    /// are the app's, so what it shows as it closes) and kept until the export's turn ends; nothing in the request
    /// was chosen from them. The note under the dialog's code options tells the user so
    /// ("export.options_code_kept"), as an option changed while the export waits its turn doesn't reach it.</remarks>
    public static void StartExport(AppState appState, ExportRequest request)
    {
        StartWriting(appState, Exports.ToWork(request, appState.Decompiler.Setup.Options));
    }

    /// <summary>Writes a portable PDB of <paramref name="assembly"/>'s decompiled C# to <paramref name="path"/>.</summary>
    /// <remarks>The decompiler's options are taken as they stand once the file is picked and kept until the export's
    /// turn ends; nothing in the request was chosen from them (the file is named for the assembly, and a PDB is always
    /// of C#).</remarks>
    public static void GeneratePdb(AppState appState, LoadedModule assembly, string path)
    {
        StartWriting(appState, new ExportWork.Pdb { Assembly = assembly, Options = appState.Decompiler.Setup.Options, File = path });
    }

    /// <summary>Writes the HTML class diagrammer of <paramref name="assembly"/> into a folder of its own in
    /// <paramref name="folder"/>, named after it, and opens it once written.</summary>
    public static void CreateDiagram(AppState appState, LoadedModule assembly, string folder)
    {
        StartWriting(appState, new ExportWork.Diagram { Assembly = assembly, Folder = Exports.GetDiagramFolder(assembly, folder) });
    }

    /// <summary>Writes a portable PDB of each open assembly into <paramref name="folder"/>, named after the assembly.
    /// Assemblies that cannot have one, or fail, are the report's problems.</summary>
    /// <remarks>The open assemblies and the decompiler's options are taken as they stand once the folder is picked
    /// and kept until the export's turn ends; nothing in the request was chosen from them.</remarks>
    public static void GeneratePdbs(AppState appState, string folder)
    {
        var work = new ExportWork.Pdbs { Assemblies = appState.ListSession.Assemblies.Assemblies, Options = appState.Decompiler.Setup.Options, Folder = folder };
        StartWriting(appState, work);
    }

    /// <summary>Writes every file in the folder of a package <paramref name="place"/> stands for, shown as
    /// <paramref name="name"/>, into <paramref name="folder"/>, at its path from that folder. Files that cannot be
    /// written there are the report's problems.</summary>
    public static void ExtractPackageFolder(AppState appState, string name, PackageFolderPlace place, string folder)
    {
        StartWriting(appState, new ExportWork.PackageFolder { Name = name, Place = place, Folder = folder });
    }

    private static void StartWriting(AppState appState, ExportWork work)
    {
        var ticket = Exports.Enter(appState.Export, appState.ViewCommands, work.Job);
        appState.Workflows.StartSettling(nameof(ExportWriter),
            ct => appState.ExportWriter.HandleAsync(appState.FinishedExports, appState.ViewCommands, work, ticket, ct),
            error => FailWriting(appState, work, ticket, error));
    }

    // An export whose workflow failed ends its turn and stays in the exports panel, failed with why, until the user
    // removes or clears it, as one whose work failed does; the edge reports it once. Its time and folder aren't known.
    private static void FailWriting(AppState appState, ExportWork work, ExportTicket ticket, Exception error)
    {
        appState.Export.Drop(ticket.Run);
        appState.FinishedExports.Add(new ExportReport
        {
            Run = ticket.Run,
            Job = ticket.Job,
            End = new ExportEnd.Failed { Subject = work.Subject, Error = error },
            Elapsed = TimeSpan.Zero,
            FolderExists = false,
        });
    }
}

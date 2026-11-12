using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.AssemblyHandling;

namespace ILSight.Exporting;

/// <summary>
/// What the export dialog asks for: which open assemblies to export, under which project names,
/// into which folder, and for several, the solution's name; besides, a strong-name key to sign
/// with and whether to write a PDB beside each project. One assembly exports as a project
/// straight into the folder, several as a solution with a folder per project. Project names start
/// out unique; only renaming can make two clash, which stops the export until it is fixed, as a
/// key file that is not there does. Everything but the ticks stays for the next time. The
/// assemblies are ticked on a page of their own, which the dialog opens on when none is ticked yet.
/// </summary>
public sealed class ExportSetup
{
    public bool IsOpen;
    public string Folder = "";
    public string SolutionName = "";

    /// <summary>The strong-name key file to copy into each project and sign it with; none when empty.</summary>
    public string StrongNameKeyFile = "";

    /// <summary>Whether a portable PDB is written beside each project.</summary>
    public bool GeneratePdb;

    /// <summary>Whether those PDBs embed the decompiled sources.</summary>
    public bool EmbedSourcesInPdb = true;

    /// <summary>Where the dialog's window is on the screen.</summary>
    public DialogWindow Window { get; } = new();

    public List<ExportChoice> Choices { get; } = new();

    public ExportPage Page;

    public AssemblyPicker Picker { get; } = new();

    /// <summary>What the disk says about <see cref="Folder"/> and <see cref="StrongNameKeyFile"/>, looked at again each
    /// time either changes.</summary>
    public ExportFileChecks Files { get; } = new();

    /// <summary>The rows of the ticked assemblies picked out on the main page, to remove together.</summary>
    public MultiSelection<ExportChoice> RowSelection { get; } = new();

    public int IncludedCount => Choices.Count(static c => c.Included);

    /// <summary>Whether the export writes a solution: more than one assembly is ticked.</summary>
    public bool IsSolution => IncludedCount > 1;

    /// <summary>Whether every assembly is ticked, none is, or some are (null).</summary>
    public bool? AllIncluded
    {
        get
        {
            var count = IncludedCount;
            if (count == 0)
            {
                return false;
            }

            if (count == Choices.Count)
            {
                return true;
            }

            return null;
        }
    }

    /// <summary>The solution's file name: the typed name, else the folder's.</summary>
    public string SolutionFileName => AssemblyExport.SolutionFileName(Exports.GetSolutionNameOrDefault(this));

    /// <summary>A project name two ticked assemblies share, which must change before exporting, or null.</summary>
    public string? Clash => ClashingChoice?.Name.Trim();

    /// <summary>The second of two ticked assemblies whose project names are the same, or null.</summary>
    public ExportChoice? ClashingChoice
    {
        get
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var choice in Choices)
            {
                if (!choice.Included)
                {
                    continue;
                }

                var name = AssemblyExport.CleanProjectName(choice.Name);
                if (!names.Add(name))
                {
                    return choice;
                }
            }
            return null;
        }
    }

    public bool HasNamelessProject => Choices.Any(static c => c.Included && c.Name.Trim().Length == 0);

    /// <summary>Whether the folder is a full path.</summary>
    public bool HasFolder => Path.IsPathFullyQualified(Folder.Trim());
}

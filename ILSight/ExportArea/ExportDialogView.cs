using System.Diagnostics;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.CommandArea;
using ILSight.ILSightAppArea;
using ILSight.AssemblyArea;

namespace ILSight.ExportArea;

/// <summary>
/// Asks what to export, in a window of its own, on two pages that each scroll as one. The main page
/// lists the ticked assemblies with their project names, then where they go, and the options; it
/// warns in place about a folder that holds files, says beside the buttons what stops the export, and
/// exports only once nothing does. The Choose page ticks assemblies among all the open ones,
/// filtered by name or path.
/// </summary>
internal static class ExportDialogView
{
    /// <summary>The assemblies the page shows, in its order. Runs each frame the page shows, so it is a loop:
    /// a lambda here would capture the picker.</summary>
    public static List<ExportChoice> ListShown(AssemblyPicker picker)
    {
        var shown = new List<ExportChoice>();
        var order = picker.Rows.Order;
        for (var i = 0; i < order.Count; i++)
        {
            var choice = order[i];
            if (IsShown(picker, choice))
            {
                shown.Add(choice);
            }
        }
        return shown;
    }

    private static bool IsShown(AssemblyPicker picker, ExportChoice choice)
    {
        if (picker.SelectedOnly && !choice.Included)
        {
            return false;
        }

        var filter = picker.Filter.Trim();
        if (filter.Length == 0)
        {
            return true;
        }

        if (choice.Assembly.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return choice.Assembly.FilePath.Text.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether every shown assembly is ticked, none is, or some are (null).</summary>
    public static bool? CalculateAllShownIncluded(AssemblyPicker picker)
    {
        var shown = ListShown(picker);
        var count = shown.Count(static c => c.Included);
        if (count == 0)
        {
            return false;
        }

        if (count == shown.Count)
        {
            return true;
        }

        return null;
    }

    /// <summary>Ticks every shown assembly when <paramref name="all"/>, else unticks them; the hidden
    /// ones stay as they are.</summary>
    public static void IncludeShown(AssemblyPicker picker, bool all)
    {
        foreach (var choice in ListShown(picker))
        {
            choice.Included = all;
        }
    }

    private const float WindowWidth = 640f;
    private const float WindowHeight = 720f;

    private const int AssemblyColumn = 0;
    private const int VersionColumn = 1;
    private const int ProjectNameColumn = 2;

    // The decompiler's names for the toggles the dialog offers.
    private const string SdkStyleProjects = "UseSdkStyleProjectFormat";
    private const string FolderPerNamespace = "UseNestedDirectoriesForNamespaces";
    private const string RemoveDeadCode = "RemoveDeadCode";
    private const string RemoveDeadStores = "RemoveDeadStores";
    private const string DebugSymbolNames = "UseDebugSymbols";

    public static void Render(Ui ui, ExportSetup exportSetup, Appearance appearance, IAppCommandWriter commands, DecompilerOptions options)
    {
        Dialog(ui, exportSetup, appearance, exportSetup.Columns, commands, options);
        exportSetup.Window.Settle(ui.Input.IsDown(PointerButton.Left));
    }

    private static void Dialog(Ui ui, ExportSetup exportSetup, Appearance appearance, TableColumns exportColumns, IAppCommandWriter commands, DecompilerOptions options)
    {
        var setup = exportSetup;
        var choosing = setup.Assemblies.Page == ExportPage.Choose;
        var title = PageTitle(appearance.Strings, setup.Assemblies.Page);
        using (var dialog = ui.WindowDialog("export-setup", title, ref setup.IsOpen, WindowWidth, WindowHeight, ref setup.Window.Bounds,
            closeOnEscape: !choosing))
        {
            if (!dialog.IsOpen)
            {
                return;
            }

            if (choosing)
            {
                ChoosePage(ui, appearance, setup);
                return;
            }
            Body(ui, exportSetup, appearance, exportColumns, commands, options);
            Footer(ui, appearance, exportSetup, commands);
        }
    }

    private static string PageTitle(Strings t, ExportPage page)
    {
        return page switch
        {
            ExportPage.Choose => t.ExportChooseTitle,
            ExportPage.Main => t.ExportDialogTitle,
            _ => throw new UnreachableException(),
        };
    }

    private static void Body(Ui ui, ExportSetup exportSetup, Appearance appearance, TableColumns exportColumns, IAppCommandWriter commands, DecompilerOptions options)
    {
        var theme = ui.Theme;
        var setup = exportSetup;
        // Scrolls when the window is too short for it, so the buttons stay in reach.
        using (ui.Scroll("export-body", new Box { Height = Sizing.Grow(), Gap = theme.DialogGap }))
        {
            Chosen(ui, appearance, exportSetup, exportColumns);
            Destination(ui, appearance, setup);
            ProjectOptions(ui, appearance, commands, options);
            CodeOptions(ui, appearance, commands, options);
            BuildOutput(ui, appearance, setup);
        }
    }

    private static Ui.Scope Section(Ui ui)
    {
        return ui.Column(new Box { Gap = ui.Theme.GapTight });
    }

    private static void Caption(Ui ui, string text)
    {
        var theme = ui.Theme;
        ui.Text(text, color: theme.TextMuted, size: theme.FontSizeSmall);
    }

    private static Box ListFrame(UiTheme theme)
    {
        return new Box()
        {
            Gap = 0f,
            Background = theme.Field,
            BorderColor = theme.Border,
            BorderWidth = theme.BorderWidth,
            Radius = theme.RadiusSmall,
            Clip = true,
        };
    }

    private static void Chosen(Ui ui, Appearance appearance, ExportSetup exportSetup, TableColumns exportColumns)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var setup = exportSetup;
        var chosen = Exports.ListIncludedChoices(setup);
        using (Section(ui))
        {
            ui.Label(t.ExportAssemblies, bold: true);
            using (ui.Column(ListFrame(theme)))
            {
                if (chosen.Count == 0)
                {
                    NoneChosen(ui, appearance);
                }
                else
                {
                    ChosenTable(ui, appearance, exportSetup, exportColumns, chosen);
                }

                ui.Separator();
                TableStrip(ui, appearance, setup);
            }
        }
    }

    private static void NoneChosen(Ui ui, Appearance appearance)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var box = new Box { Padding = new Spacing(theme.Gap, theme.GapTight) };
        ui.Text(t.ExportNoneChosen, box, color: theme.TextMuted);
    }

    private static void ChosenTable(Ui ui, Appearance appearance, ExportSetup exportSetup, TableColumns exportColumns, List<ExportChoice> chosen)
    {
        var t = appearance.Strings;
        var setup = exportSetup;
        var table = ui.Table("chosen", exportColumns, setup.RowSelection);
        table.Header([t.ExportAssemblyColumn, t.ExportVersionColumn, t.ExportProjectName]);
        var removeRequested = false;
        using (var rows = table.FitBody(chosen))
        {
            removeRequested = rows.RemoveRequested;
            var clashing = setup.ClashingChoice;
            for (var i = 0; i < chosen.Count; i++)
            {
                ChosenRow(ui, appearance, rows, i, setup, chosen, clashing);
            }
        }
        // The rows picked out go only once they are all recorded.
        if (removeRequested)
        {
            Exports.RemoveSelectedRows(setup);
        }
    }

    private static void ChosenRow(Ui ui, Appearance appearance, TableRows<ExportChoice> rows, int index, ExportSetup setup, List<ExportChoice> chosen,
        ExportChoice? clashing)
    {
        var t = appearance.Strings;
        var choice = chosen[index];
        using var row = rows.Row(index);
        using (row.Cell(AssemblyColumn))
        {
            AssemblyName(ui, choice);
        }

        using (row.Cell(VersionColumn))
        {
            VersionText(ui, choice);
        }

        using (row.Cell(ProjectNameColumn))
        {
            using (ui.Column(new Box { Width = Sizing.Grow(), Gap = 0f }))
            {
                ui.TextField("name", ref choice.Name, t.ExportProjectName, Sizing.Grow(), look: FieldLook.Cell);
                if (choice == clashing)
                {
                    ClashNote(ui, appearance, choice);
                }
            }
        }
        ui.Tooltip(row.Id, choice.Assembly.FilePath.Text, TooltipAnchor.Pointer);
        PickOutWhileEditing(ui, setup, chosen, choice);
    }

    // The row being edited is picked out alone, so what Remove takes is never in doubt.
    private static void PickOutWhileEditing(Ui ui, ExportSetup setup, List<ExportChoice> chosen, ExportChoice choice)
    {
        var nameId = ui.GetId("name");
        if (!ui.HasFocus(nameId))
        {
            return;
        }

        if (setup.RowSelection.IsOnly(choice))
        {
            return;
        }

        setup.RowSelection.Select(choice, SelectionChange.Replace, chosen);
    }

    private static void AssemblyName(Ui ui, ExportChoice choice)
    {
        ui.Text(choice.Assembly.Name, new Box { Width = Sizing.Grow() }, overflow: TextOverflow.Ellipsis);
    }

    private static void VersionText(Ui ui, ExportChoice choice)
    {
        var theme = ui.Theme;
        var version = choice.Assembly.Version;
        if (version is null)
        {
            return;
        }

        ui.Text(version.ToString(), new Box { Width = Sizing.Grow() }, color: theme.TextMuted, overflow: TextOverflow.Ellipsis);
    }

    private static void ClashNote(Ui ui, Appearance appearance, ExportChoice choice)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var box = new Box { Padding = new Spacing(theme.FieldPaddingX + theme.BorderWidth, 0f, 0f, theme.GapTight) };
        ui.Text(t.ExportNameClash(choice.Name.Trim()), box, color: theme.Danger, size: theme.FontSizeSmall, wrap: true);
    }

    private static void TableStrip(Ui ui, Appearance appearance, ExportSetup setup)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        using (ui.Row(new Box { Gap = theme.GapTight, Padding = new Spacing(theme.GapTight, theme.GapTight, theme.Gap, theme.GapTight) }))
        {
            if (ui.IconButton("add", appearance.Icons.ExportAdd, t.ExportAdd))
            {
                setup.ChooseAssemblies();
            }

            var anyPicked = setup.RowSelection.Count > 0;
            if (ui.IconButton("remove-selected", appearance.Icons.ExportRemovePicked, t.ExportRemoveSelected, enabled: anyPicked))
            {
                Exports.RemoveSelectedRows(setup);
            }

            ui.Leaf(new Box { Width = Sizing.Grow(), Height = 0f });
            SelectedCount(ui, appearance, setup);
        }
    }

    private static void SelectedCount(Ui ui, Appearance appearance, ExportSetup setup)
    {
        var t = appearance.Strings;
        Caption(ui, t.ExportSelectedCount(selected: setup.IncludedCount, total: setup.Assemblies.Choices.Count));
    }

    private static void Destination(Ui ui, Appearance appearance, ExportSetup setup)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        using (Section(ui))
        {
            ui.Label(t.ExportDestination, bold: true);
            using (ui.Row(new Box { Gap = theme.GapTight }))
            {
                ui.TextField("folder", ref setup.Folder, t.ExportFolderPlaceholder, Sizing.Grow());
                if (ui.Button(t.ExportBrowse, id: "browse"))
                {
                    PickFolder(ui, appearance, setup);
                }
            }
            var files = FindFacts(ui, setup);
            if (files?.FolderHoldsFiles == true)
            {
                ui.Text(t.ExportFolderWarning, color: theme.Warning, size: theme.FontSizeSmall, wrap: true);
            }

            SolutionName(ui, appearance, setup);
        }
    }

    private static void SolutionName(Ui ui, Appearance appearance, ExportSetup setup)
    {
        if (!setup.IsSolution)
        {
            return;
        }

        var t = appearance.Strings;
        var placeholder = ui.Memo("export-solution-default", setup.Folder, static f => Exports.GetDefaultSolutionName(f));
        Caption(ui, t.ExportSolution);
        ui.TextField("solution", ref setup.SolutionName, placeholder, width: Sizing.Grow());
    }

    // Kept with the decompiler options, so they stay for the next export, but only shown here.
    private static void ProjectOptions(Ui ui, Appearance appearance, IAppCommandWriter commands, DecompilerOptions options)
    {
        var t = appearance.Strings;
        using (Section(ui))
        {
            ui.Label(t.ExportOptionsProject, bold: true);
            Toggle(ui, appearance, commands, options, SdkStyleProjects);
            Toggle(ui, appearance, commands, options, FolderPerNamespace);
        }
    }

    // The same options as in the decompiler options panel, so changing them changes the code being browsed too, which
    // the note under the heading says before they are changed; the second note says the export keeps them as they are
    // when Export is clicked, so a change while it waits its turn doesn't reach it (ExportCommands.StartExport).
    private static void CodeOptions(Ui ui, Appearance appearance, IAppCommandWriter commands, DecompilerOptions options)
    {
        var t = appearance.Strings;
        using (Section(ui))
        {
            ui.Label(t.ExportOptionsCode, bold: true);
            Caption(ui, t.ExportOptionsCodeNote);
            Caption(ui, t.ExportOptionsCodeKept);
            Toggle(ui, appearance, commands, options, RemoveDeadCode);
            Toggle(ui, appearance, commands, options, RemoveDeadStores);
            Toggle(ui, appearance, commands, options, DebugSymbolNames);
        }
    }

    private static void Toggle(Ui ui, Appearance appearance, IAppCommandWriter commands, DecompilerOptions options, string name)
    {
        var t = appearance.Strings;
        var toggle = DecompilerToggles.FindToggle(name);
        if (toggle is null)
        {
            return;
        }

        var label = DecompilerOptionLabels.Label(t, name) ?? name;
        var on = DecompilerToggles.IsOn(options, toggle);
        if (ui.Checkbox(label, ref on, id: name, compact: true))
        {
            Commands.Later(commands, (toggle, on), static (a, s) => Preferences.SetDecompilerToggle(a, s.toggle, s.on));
        }
    }

    private static void BuildOutput(Ui ui, Appearance appearance, ExportSetup setup)
    {
        var t = appearance.Strings;
        using (Section(ui))
        {
            ui.Label(t.ExportBuildOutput, bold: true);
            Signing(ui, appearance, setup);
            Pdbs(ui, appearance, setup);
        }
    }

    // Its own method, so that the closure is made only the frame Browse is pressed.
    private static void PickFolder(Ui ui, Appearance appearance, ExportSetup setup)
    {
        ui.PickFolder(appearance.Strings.ExportChooseFolderTitle, setup.Folder, path => setup.Folder = path);
    }

    // Its own method, so that the closure is made only the frame Browse is pressed.
    private static void PickKeyFile(Ui ui, Appearance appearance, ExportSetup setup)
    {
        var t = appearance.Strings;
        ui.PickFile(t.ExportChooseKeyTitle, null, [new FileFilter(t.FileFilterSnk, ["*.snk"])], path => setup.StrongNameKeyFile = path);
    }

    private static void Signing(Ui ui, Appearance appearance, ExportSetup setup)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        Caption(ui, t.ExportKeyFile);
        using (ui.Row(new Box { Gap = theme.GapTight }))
        {
            ui.TextField("key-file", ref setup.StrongNameKeyFile, t.ExportKeyFileNone, Sizing.Grow());
            if (ui.Button(t.ExportBrowse, id: "browse-key"))
            {
                PickKeyFile(ui, appearance, setup);
            }
        }
        var files = FindFacts(ui, setup);
        if (files?.IsKeyFileMissing == true)
        {
            ui.Text(t.ExportKeyFileMissing, color: theme.Danger, size: theme.FontSizeSmall, wrap: true);
        }
    }

    private static void Pdbs(Ui ui, Appearance appearance, ExportSetup setup)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        ui.Checkbox(t.ExportGeneratePdb, ref setup.GeneratePdb, id: "pdb", compact: true);
        if (!setup.GeneratePdb)
        {
            return;
        }

        using (ui.Row(new Box { Padding = new Spacing(theme.CheckboxSize + theme.Gap, 0f, 0f, 0f) }))
        {
            ui.Checkbox(t.ExportEmbedSources, ref setup.EmbedSourcesInPdb, id: "embed", compact: true);
        }
    }

    private static void Footer(Ui ui, Appearance appearance, ExportSetup exportSetup, IAppCommandWriter commands)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var setup = exportSetup;
        ui.Separator();
        using (ui.Row(new Box { Gap = theme.Gap }))
        {
            var blocker = Exports.FindBlocker(setup, FindFiles(ui, setup));
            var color = IsMistake(blocker) ? theme.Danger : theme.TextMuted;
            ui.Text(BlockerText(t, blocker), color: color, size: theme.FontSizeSmall, wrap: true);
            if (blocker is ExportBlocker.CheckFailed && ui.Button(t.NugetRetry, id: "export-check-retry", variant: ButtonVariant.Ghost))
            {
                RetryCheck(commands, setup.Files);
            }

            if (ui.Button(t.ExportCancel, id: "export-cancel", variant: ButtonVariant.Ghost))
            {
                setup.Close();
            }

            if (ui.Button(t.ExportConfirm, enabled: blocker is null, id: "export-start", variant: ButtonVariant.Primary))
            {
                Commands.Later(commands, setup, static (a, s) => Start(s, a));
            }
        }
    }

    // The disk is asked once the frame is drawn, but the dialog shows it being looked at from this frame on.
    private static void RetryCheck(IAppCommandWriter commands, ExportFileChecks files)
    {
        var check = files.Retry();
        if (check is null)
        {
            return;
        }

        Commands.StartSettling(commands, nameof(CheckExportFilesHandler), (Files: files, Check: check),
            static (a, s, ct) => a.CheckExportFilesHandler.HandleAsync(s.Files, s.Check, ct), static (_, s, error) => s.Files.Fail(s.Check, error.Message));
    }

    // A mistake is something wrong rather than not yet given: typed wrong, or the disk failing to say.
    private static bool IsMistake(ExportBlocker? blocker)
    {
        return blocker is ExportBlocker.Clash or ExportBlocker.KeyFileMissing or ExportBlocker.CheckFailed;
    }

    private static string BlockerText(Strings t, ExportBlocker? blocker)
    {
        return blocker switch
        {
            null => "",
            ExportBlocker.NoAssembly => t.ExportNeedsAssembly,
            ExportBlocker.NoFolder => t.ExportNeedsFolder,
            ExportBlocker.NamelessProject => t.ExportNeedsName,
            ExportBlocker.Clash clash => t.ExportNameClash(clash.Name),
            ExportBlocker.KeyFileMissing => t.ExportKeyFileMissing,
            ExportBlocker.Checking => "",
            ExportBlocker.CheckFailed failed => failed.Why,
        };
    }

    private static void ChoosePage(Ui ui, Appearance appearance, ExportSetup setup)
    {
        var theme = ui.Theme;
        if (ui.Input.TakeKey(KeyboardKey.Escape))
        {
            setup.Assemblies.FinishChoosing();
        }

        using (ui.Column(new Box { Height = Sizing.Grow(), Gap = theme.Gap }))
        {
            FilterRow(ui, appearance, setup.Picker);
            SelectShownRow(ui, appearance, setup.Picker);
            PickList(ui, appearance, setup.Picker);
        }
        ChooseFooter(ui, appearance, setup);
    }

    private static void FilterRow(Ui ui, Appearance appearance, AssemblyPicker picker)
    {
        var t = appearance.Strings;
        using (ui.Row())
        {
            ui.TextField("filter", ref picker.Filter, t.ExportFilter, Sizing.Grow(), appearance.Icons.ExportFilter, clearable: true);
            ui.Checkbox(t.ExportSelectedOnly, ref picker.SelectedOnly, id: "selected-only");
        }
    }

    private static void SelectShownRow(Ui ui, Appearance appearance, AssemblyPicker picker)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        // Padded to line up with the rows' boxes.
        using (ui.Row(new Box { Padding = new Spacing(theme.Gap + theme.BorderWidth, 0f, 0f, 0f) }))
        {
            var all = CalculateAllShownIncluded(picker);
            if (ui.Checkbox(t.ExportSelectShown, ref all, id: "all", compact: true))
            {
                IncludeShown(picker, all == true);
            }
        }
    }

    private static void PickList(Ui ui, Appearance appearance, AssemblyPicker picker)
    {
        var theme = ui.Theme;
        var shown = ListShown(picker);
        using (ui.Column(ListFrame(theme) with { Height = Sizing.Grow() }))
        {
            if (shown.Count == 0)
            {
                NoMatch(ui, appearance, picker);
                return;
            }
            using (ui.Scroll("export-choices", new Box { Height = Sizing.Grow(), Padding = new Spacing(0f, theme.GapTight) }))
            {
                foreach (var choice in shown)
                {
                    PickRow(ui, picker, shown, choice);
                }
            }
        }
    }

    private static void NoMatch(Ui ui, Appearance appearance, AssemblyPicker picker)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        using (ui.Column(new Box { Height = Sizing.Grow(), Gap = theme.Gap, AlignX = Align.Center, AlignY = Align.Center }))
        {
            ui.Text(t.ExportNoMatch, color: theme.TextMuted);
            if (ui.Button(t.ExportClearFilter, id: "clear-filter", variant: ButtonVariant.Ghost))
            {
                picker.ClearFilter();
            }
        }
    }

    private static void PickRow(Ui ui, AssemblyPicker picker, List<ExportChoice> shown, ExportChoice choice)
    {
        var theme = ui.Theme;
        using (ui.Id(choice.Assembly.FilePath.Text))
        using (ui.Row(new Box { Gap = theme.Gap, Padding = new Spacing(theme.Gap, 0f) }))
        {
            var width = Sizing.Grow();
            if (ui.Checkbox(choice.Assembly.Name, ref choice.Included, id: "include", width: width, compact: true))
            {
                picker.Rows.TickClicked(shown, choice, (ui.Input.Modifiers & KeyModifiers.Shift) != 0);
            }

            ui.Tooltip(ui.GetId("include"), choice.Assembly.FilePath.Text, TooltipAnchor.Pointer);
            var version = choice.Assembly.Version;
            if (version is not null)
            {
                Caption(ui, version.ToString());
            }
        }
    }

    private static void ChooseFooter(Ui ui, Appearance appearance, ExportSetup setup)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        ui.Separator();
        using (ui.Row(new Box { Gap = theme.Gap }))
        {
            SelectedCount(ui, appearance, setup);
            ui.Leaf(new Box { Width = Sizing.Grow(), Height = 0f });
            if (ui.Button(t.ExportDone, id: "done", variant: ButtonVariant.Primary))
            {
                setup.Assemblies.FinishChoosing();
            }
        }
    }

    /// <summary>Closes the dialog and exports what it asks for, unless something stops the export, or what the disk
    /// says about the folder and key file it names is still being looked at.</summary>
    internal static void Start(ExportSetup setup, AppState appState)
    {
        var files = setup.Files.FindLook(setup.Folder, setup.StrongNameKeyFile);
        if (!Exports.CanExport(setup, files))
        {
            return;
        }

        setup.Close();
        ExportCommands.StartExport(appState, Exports.CreateRequest(setup));
    }

    private static ExportFileFacts? FindFacts(Ui ui, ExportSetup setup)
    {
        var look = FindFiles(ui, setup);
        if (look is not ExportFileLook.Checked looked)
        {
            return null;
        }

        return looked.Facts;
    }

    private static ExportFileLook? FindFiles(Ui ui, ExportSetup setup)
    {
        var look = setup.Files.FindLook(setup.Folder, setup.StrongNameKeyFile);
        if (look is null)
        {
            // The app loop starts the look (ExportFileChecker), so a frame is asked for.
            ui.RequestUpdate();
        }

        return look;
    }
}

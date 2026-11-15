using ZGF.Desktop;
using ZGF.Gui.Hosting;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.DocumentViewArea;
using ILSight.Core;
using ILSight.ExportArea;
using ILSight.CommandArea;
using ILSight.ProcessArea;
using ILSight.FrameworkArea;
using ILSight.UserArea;
using ILSight.NuGetArea;

namespace ILSight.MainArea;

internal static class MainMenuView
{
    private const string AppIconId = "title-bar-icon";

    /// <summary>The menus: in the system's menu bar where there is one, else in the window's title bar
    /// where the platform allows, unless the system's title bar was chosen, and else in a bar of their own.</summary>
    public static void Render(Ui ui, Appearance appearance, IAppCommandWriter commands, MainLayout mainLayout, DocumentViews documents, Panels panels, Confirmation layoutReset, AboutDialog about, BrowseTree tree, ScreenDisplay display, ErrorDialog errorDialog, FrameworkPicker frameworkPicker, NuGetPicker nuGetPicker, ReadNuGetSetupHandler readNuGetSetupHandler, ProcessPicker processPicker, ExportSetup exportSetup, AppState appState)
    {
        if (ui.SystemMenus is not null)
        {
            RenderSystemMenuBar(ui, appearance, commands, mainLayout, documents, panels, layoutReset, about, tree, errorDialog, frameworkPicker, nuGetPicker, readNuGetSetupHandler, processPicker, exportSetup, appState);
        }
        else if (IsInTitleBar(ui, display))
        {
            RenderInTitleBar(ui, appearance, commands, mainLayout, documents, panels, layoutReset, about, tree, errorDialog, frameworkPicker, nuGetPicker, readNuGetSetupHandler, processPicker, exportSetup, appState);
        }
        else
        {
            RenderMenuBar(ui, appearance, commands, mainLayout, documents, panels, layoutReset, about, tree, errorDialog, frameworkPicker, nuGetPicker, readNuGetSetupHandler, processPicker, exportSetup, appState);
        }
    }

    private static bool IsInTitleBar(Ui ui, ScreenDisplay display)
    {
        if (display.Options.SystemTitleBar)
        {
            return false;
        }

        return ui.CanDrawTitleBar;
    }

    private static void RenderInTitleBar(Ui ui, Appearance appearance, IAppCommandWriter commands, MainLayout mainLayout, DocumentViews documents, Panels panels, Confirmation layoutReset, AboutDialog about, BrowseTree tree, ErrorDialog errorDialog, FrameworkPicker frameworkPicker, NuGetPicker nuGetPicker, ReadNuGetSetupHandler readNuGetSetupHandler, ProcessPicker processPicker, ExportSetup exportSetup, AppState appState)
    {
        var theme = ui.Theme;
        var bar = new Box { Background = theme.Background, Gap = 0f, Padding = new Spacing(theme.Gap, 0f, 0f, 0f) };
        using (ui.TitleBar("title-bar", bar))
        {
            var menus = new Box
            {
                Width = Sizing.Fit(),
                Height = Sizing.Grow(),
                Padding = new Spacing(theme.GapTight),
                Background = theme.Background,
            };
            using (ui.MenuBar("menu", menus))
            {
                WindowMenu(ui, appearance);
                Menus(ui, appearance, commands, mainLayout, documents, panels, layoutReset, about, tree, errorDialog, frameworkPicker, nuGetPicker, readNuGetSetupHandler, processPicker, exportSetup, appState);
            }
            ui.TitleBarDragArea("title-bar-drag");
        }
    }

    private static void WindowMenu(Ui ui, Appearance appearance)
    {
        var t = appearance.Strings;
        using var menu = ui.AppIconMenu(AppIconId);
        if (!menu.IsOpen)
        {
            return;
        }

        var icons = appearance.Icons;
        var maximized = ui.WindowBounds.Maximized;
        if (ui.MenuItem(t.WindowRestore, enabled: maximized, id: "restore", icon: new UiIcon(icons.WindowRestore)))
        {
            ui.RestoreWindow();
        }

        if (ui.MenuItem(t.WindowMinimize, id: "minimize", icon: new UiIcon(icons.WindowMinimize)))
        {
            ui.MinimizeWindow();
        }

        if (ui.MenuItem(t.WindowMaximize, enabled: !maximized, id: "maximize", icon: new UiIcon(icons.WindowMaximize)))
        {
            ui.MaximizeWindow();
        }

        ui.MenuSeparator();
        if (ui.MenuItem(t.WindowClose, Shortcuts.CloseWindow, id: "close", icon: new UiIcon(icons.WindowClose)))
        {
            ui.Quit();
        }
    }

    private static void RenderSystemMenuBar(Ui ui, Appearance appearance, IAppCommandWriter commands, MainLayout mainLayout, DocumentViews documents, Panels panels, Confirmation layoutReset, AboutDialog about, BrowseTree tree, ErrorDialog errorDialog, FrameworkPicker frameworkPicker, NuGetPicker nuGetPicker, ReadNuGetSetupHandler readNuGetSetupHandler, ProcessPicker processPicker, ExportSetup exportSetup, AppState appState)
    {
        var t = appearance.Strings;
        using (ui.MenuBar("menu"))
        {
            using (ui.Menu(AboutDialogView.AppName, id: "app"))
            {
                AppMenu(ui, appearance, panels, about);
            }

            FileAndViewMenus(ui, appearance, commands, mainLayout, documents, panels, layoutReset, tree, errorDialog, frameworkPicker, nuGetPicker, readNuGetSetupHandler, processPicker, exportSetup, appState);
            using (ui.Menu(t.MenuWindow, id: "window", role: AppMenuRole.Window))
            {
                SystemWindowMenu(ui, appearance);
            }

            // Left empty: macOS gives a Help menu a search of every item.
            using (ui.Menu(t.MenuHelp, id: "help", role: AppMenuRole.Help))
            {
            }
        }
    }

    private static void AppMenu(Ui ui, Appearance appearance, Panels panels, AboutDialog about)
    {
        var t = appearance.Strings;
        if (ui.MenuItem(t.HelpAbout, id: "about"))
        {
            about.Open();
        }

        ui.MenuSeparator();
        if (ui.MenuItem(t.AppMenuSettings, Shortcuts.Settings, id: "settings"))
        {
            panels.Open(DockView.DisplayOptions);
        }

        ui.MenuSeparator();
        // Left empty: macOS fills it, as it does for every app.
        using (ui.SubMenu(t.AppMenuServices, id: "services", role: AppMenuRole.Services))
        {
        }
        ui.MenuSeparator();
        ui.SystemMenuItem(t.AppMenuHide, AppMenuSystemAction.Hide, Shortcuts.Hide);
        ui.SystemMenuItem(t.AppMenuHideOthers, AppMenuSystemAction.HideOthers, Shortcuts.HideOthers);
        ui.SystemMenuItem(t.AppMenuShowAll, AppMenuSystemAction.ShowAll);
        ui.MenuSeparator();
        ui.SystemMenuItem(t.AppMenuQuit, AppMenuSystemAction.Quit, Shortcuts.Quit);
    }

    // macOS ends this menu with the open windows.
    private static void SystemWindowMenu(Ui ui, Appearance appearance)
    {
        var t = appearance.Strings;
        ui.SystemMenuItem(t.WindowMinimize, AppMenuSystemAction.Minimize, Shortcuts.Minimize);
        ui.SystemMenuItem(t.WindowZoom, AppMenuSystemAction.Zoom);
        ui.MenuSeparator();
        ui.SystemMenuItem(t.WindowBringAllToFront, AppMenuSystemAction.BringAllToFront);
    }

    private static void RenderMenuBar(Ui ui, Appearance appearance, IAppCommandWriter commands, MainLayout mainLayout, DocumentViews documents, Panels panels, Confirmation layoutReset, AboutDialog about, BrowseTree tree, ErrorDialog errorDialog, FrameworkPicker frameworkPicker, NuGetPicker nuGetPicker, ReadNuGetSetupHandler readNuGetSetupHandler, ProcessPicker processPicker, ExportSetup exportSetup, AppState appState)
    {
        using (ui.MenuBar("menu", new Box { Background = ui.Theme.Background }))
        {
            Menus(ui, appearance, commands, mainLayout, documents, panels, layoutReset, about, tree, errorDialog, frameworkPicker, nuGetPicker, readNuGetSetupHandler, processPicker, exportSetup, appState);
        }
    }

    private static void Menus(Ui ui, Appearance appearance, IAppCommandWriter commands, MainLayout mainLayout, DocumentViews documents, Panels panels, Confirmation layoutReset, AboutDialog about, BrowseTree tree, ErrorDialog errorDialog, FrameworkPicker frameworkPicker, NuGetPicker nuGetPicker, ReadNuGetSetupHandler readNuGetSetupHandler, ProcessPicker processPicker, ExportSetup exportSetup, AppState appState)
    {
        var t = appearance.Strings;
        FileAndViewMenus(ui, appearance, commands, mainLayout, documents, panels, layoutReset, tree, errorDialog, frameworkPicker, nuGetPicker, readNuGetSetupHandler, processPicker, exportSetup, appState);
        using (var help = ui.Menu(t.MenuHelp, id: "help"))
        {
            if (help.IsOpen)
            {
                HelpMenu(ui, appearance, about);
            }
        }
    }

    private static void FileAndViewMenus(Ui ui, Appearance appearance, IAppCommandWriter commands, MainLayout mainLayout, DocumentViews documents, Panels panels, Confirmation layoutReset, BrowseTree tree, ErrorDialog errorDialog, FrameworkPicker frameworkPicker, NuGetPicker nuGetPicker, ReadNuGetSetupHandler readNuGetSetupHandler, ProcessPicker processPicker, ExportSetup exportSetup, AppState appState)
    {
        var t = appearance.Strings;
        using (var file = ui.Menu(t.MenuFile, id: "file"))
        {
            if (file.IsOpen)
            {
                FileMenu(ui, appearance, commands, tree, frameworkPicker, nuGetPicker, readNuGetSetupHandler, processPicker, exportSetup, appState);
            }
        }
        using (var view = ui.Menu(t.MenuView, id: "view"))
        {
            if (view.IsOpen)
            {
                ViewMenu(ui, appearance, commands, mainLayout, documents, panels, layoutReset, errorDialog, appState);
            }
        }
    }

    /// <summary>Asks which file to open; the file picked begins a request in <paramref name="navigation"/> as it is
    /// answered, and only opening it is queued.</summary>
    public static void OpenFile(Ui ui, Appearance appearance, IAppCommandWriter commands, Navigation navigation)
    {
        var t = appearance.Strings;
        FileFilter[] filters =
        [
            new(t.FileFilterAssembliesAndPackages, ["*.dll", "*.exe", "*.winmd", "*.wasm", "*.nupkg", "*.zip"]),
            new(t.FileFilterAll, ["*.*"]),
        ];
        ui.PickFile(t.FileOpenTitle, null, filters, path => OpenPicked(commands, navigation, path));
    }

    private static void OpenPicked(IAppCommandWriter commands, Navigation navigation, string path)
    {
        var file = AssemblyPath.Parse(path);
        if (file is null)
        {
            return;
        }

        Commands.OpenFilesLater(commands, navigation.BeginOpening([file]));
    }

    private static void FileMenu(Ui ui, Appearance appearance, IAppCommandWriter commands, BrowseTree tree, FrameworkPicker frameworkPicker, NuGetPicker nuGetPicker, ReadNuGetSetupHandler readNuGetSetupHandler, ProcessPicker processPicker, ExportSetup exportSetup, AppState appState)
    {
        var t = appearance.Strings;
        var icons = appearance.Icons;
        if (ui.MenuItem(t.FileOpen, Shortcuts.Open, id: "open", icon: new UiIcon(icons.FileOpen)))
        {
            OpenFile(ui, appearance, commands, appState.ListSession.Navigation);
        }

        if (ui.MenuItem(t.FileOpenFromFramework, id: "open-from-framework", icon: new UiIcon(icons.FileOpenFromFramework)))
        {
            FrameworkPickerView.Open(commands, frameworkPicker);
        }

        if (ui.MenuItem(t.FileOpenFromNuget, id: "open-from-nuget", icon: new UiIcon(icons.FileOpenFromNuGet)))
        {
            NuGetPickerView.Open(commands, nuGetPicker, readNuGetSetupHandler);
        }

        if (ui.MenuItem(t.FileOpenFromProcess, id: "open-from-process", icon: new UiIcon(icons.FileOpenFromProcess)))
        {
            ProcessPickerView.Open(commands, processPicker);
        }

        ui.MenuSeparator();
        ExportItems(ui, appearance, commands, tree, exportSetup, appState);
    }

    private static void ExportItems(Ui ui, Appearance appearance, IAppCommandWriter commands, BrowseTree tree, ExportSetup exportSetup, AppState appState)
    {
        var t = appearance.Strings;
        var canSave = Exports.CanSaveCode(appState.ListSession.Navigation.ActiveTab?.Node);
        if (ui.MenuItem(t.FileSaveCode, Shortcuts.SaveCode, enabled: canSave, id: "save-code", icon: new UiIcon(appearance.Icons.FileSaveCode)))
        {
            SaveDialogs.PickActiveCodeFile(ui, appearance, commands, appState.ListSession.Navigation, appState.Decompiler.Setup.Language);
        }

        var anyOpen = tree.Roots.Count > 0;
        if (ui.MenuItem(t.FileExport, Shortcuts.Export, enabled: anyOpen, id: "export", icon: new UiIcon(appearance.Icons.FileExportProject)))
        {
            exportSetup.Open(appState.ListSession.Assemblies.Assemblies, Exports.FindActiveAssembly(appState.ListSession.Navigation));
        }

        if (ui.MenuItem(t.FileGeneratePdbs, enabled: anyOpen, id: "generate-pdbs", icon: new UiIcon(appearance.Icons.FileGeneratePdb)))
        {
            SaveDialogs.PickPdbFolder(ui, appearance, commands);
        }
    }

    private static void ViewMenu(Ui ui, Appearance appearance, IAppCommandWriter commands, MainLayout mainLayout, DocumentViews documents, Panels panels, Confirmation layoutReset, ErrorDialog errorDialog, AppState appState)
    {
        var t = appearance.Strings;
        if (ui.MenuItem(t.ViewBack, Shortcuts.Back, enabled: appState.ListSession.Navigation.CanGoBack, id: "back"))
        {
            Commands.MoveLater(commands, appState.ListSession.Navigation.GoBack(appState.ListSession.Assemblies, appState.FinishedExports, appState.Decompiler.Setup));
        }

        if (ui.MenuItem(t.ViewForward, Shortcuts.Forward, enabled: appState.ListSession.Navigation.CanGoForward, id: "forward"))
        {
            Commands.MoveLater(commands, appState.ListSession.Navigation.GoForward(appState.ListSession.Assemblies, appState.FinishedExports, appState.Decompiler.Setup));
        }

        ui.MenuSeparator();
        DecompilerMenu(ui, appearance, commands, mainLayout, documents, appState.Decompiler);
        ui.MenuSeparator();
        ApiVisibilityMenu(ui, appearance, commands, appState.ApiVisibility);
        ui.MenuSeparator();
        PanelsMenu(ui, appearance, mainLayout, panels);
        if (ui.MenuItem(t.ViewCloseAllDocuments, enabled: appState.ListSession.Navigation.OpenTabs.Count > 0, id: "close-all-documents"))
        {
            DocumentPanels.CloseAllTabs(mainLayout, documents);
        }

        if (ui.MenuItem(t.ViewResetLayout, id: "reset-layout"))
        {
            layoutReset.Ask();
        }

        // The errors recorded open when asked, here or from the status bar's indicator, never by themselves.
        if (ui.MenuItem(t.ViewErrors, enabled: appState.Errors.Entries.Count > 0, id: "errors"))
        {
            errorDialog.Open(appState.Errors.Entries);
        }

        ui.MenuSeparator();
        ThemeMenu(ui, appearance, commands, appState.Themes, appState.ChosenThemes.Theme);
        LanguageMenu(ui, appearance, commands, appState.Locale);
    }

    private static void ThemeMenu(Ui ui, Appearance appearance, IAppCommandWriter commands, ThemeCatalog<ThemeFile, ThemeContents, AppTheme> catalog, ThemeId<ThemeFile> current)
    {
        var t = appearance.Strings;
        using var themes = ui.SubMenu(t.ViewTheme, id: "theme");
        if (!themes.IsOpen)
        {
            return;
        }

        var names = catalog.ListNames(t.Culture);
        for (var i = 0; i < catalog.Ids.Count; i++)
        {
            var id = catalog.Ids[i];
            if (ui.MenuRadioItem(names[i], current == id, id: id.Text))
            {
                Commands.Later(commands, id, static (a, theme) => a.ChosenThemes.ChooseTheme(theme));
            }
        }
    }

    private static void ApiVisibilityMenu(Ui ui, Appearance appearance, IAppCommandWriter commands, ApiVisibility current)
    {
        var t = appearance.Strings;
        using var shown = ui.SubMenu(t.ViewShownApi, id: "shown-api");
        if (!shown.IsOpen)
        {
            return;
        }

        ApiVisibilityItem(ui, commands, current, t.ViewShownPublicOnly, ApiVisibility.PublicOnly);
        ApiVisibilityItem(ui, commands, current, t.ViewShownSourceDeclared, ApiVisibility.AllButCompilerGenerated);
        ApiVisibilityItem(ui, commands, current, t.ViewShownAll, ApiVisibility.All);
    }

    private static void ApiVisibilityItem(Ui ui, IAppCommandWriter commands, ApiVisibility current, string label, ApiVisibility visibility)
    {
        var selected = current == visibility;
        if (ui.MenuRadioItem(label, selected))
        {
            Commands.Later(commands, visibility, static (a, v) => a.ApiVisibility = v);
        }
    }

    private static void PanelsMenu(Ui ui, Appearance appearance, MainLayout mainLayout, Panels panels)
    {
        var t = appearance.Strings;
        using var menu = ui.SubMenu(t.ViewPanels, id: "panels");
        if (!menu.IsOpen)
        {
            return;
        }

        if (PanelItem(ui, mainLayout, appearance, t.PanelAssemblies, DockView.Assemblies))
        {
            panels.Open(DockView.Assemblies);
        }

        if (PanelItem(ui, mainLayout, appearance, t.ViewBookmarks, DockView.Bookmarks))
        {
            panels.Open(DockView.Bookmarks);
        }

        if (PanelItem(ui, mainLayout, appearance, t.PanelAnalyzer, DockView.Analyzer))
        {
            panels.Open(DockView.Analyzer);
        }

        if (PanelItem(ui, mainLayout, appearance, t.PanelSearch, DockView.Search, Shortcuts.Search))
        {
            panels.OpenSearch();
        }

        if (PanelItem(ui, mainLayout, appearance, t.PanelDecompilerOptions, DockView.DecompilerOptions))
        {
            panels.Open(DockView.DecompilerOptions);
        }

        if (PanelItem(ui, mainLayout, appearance, t.PanelDisplayOptions, DockView.DisplayOptions))
        {
            panels.Open(DockView.DisplayOptions);
        }

        if (PanelItem(ui, mainLayout, appearance, t.PanelExports, DockView.Exports))
        {
            panels.Open(DockView.Exports);
        }
    }

    private static bool PanelItem(Ui ui, MainLayout mainLayout, Appearance appearance, string label, DockView view, Shortcut? shortcut = null)
    {
        var panel = DockPanels.NameOf(view);
        var open = mainLayout.Dock.Contains(panel);
        return ui.MenuItem(label, shortcut, check: open, id: panel, icon: DockPanels.Icon(appearance.Icons, view));
    }

    private static void DecompilerMenu(Ui ui, Appearance appearance, IAppCommandWriter commands, MainLayout mainLayout, DocumentViews documents, DecompilerChoice decompiler)
    {
        var current = decompiler.Setup.Language;
        var t = appearance.Strings;
        using var decompilers = ui.SubMenu(t.ViewDecompiler, id: "decompiler");
        if (!decompilers.IsOpen)
        {
            return;
        }

        var languages = DecompilationLanguages.All;
        for (var i = 0; i < languages.Count; i++)
        {
            var language = languages[i];
            var label = DecompilationLanguages.Label(language);
            if (ui.MenuRadioItem(label, current == language))
            {
                DocumentPanels.ShowInLanguage(mainLayout, documents, commands, decompiler, language);
            }
        }
    }

    private static void HelpMenu(Ui ui, Appearance appearance, AboutDialog aboutDialog)
    {
        var t = appearance.Strings;
        if (ui.MenuItem(t.HelpAbout, id: "about"))
        {
            aboutDialog.Open();
        }
    }

    private static void LanguageMenu(Ui ui, Appearance appearance, IAppCommandWriter commands, Locale current)
    {
        var t = appearance.Strings;
        using var languages = ui.SubMenu(t.ViewLanguage, id: "language");
        if (!languages.IsOpen)
        {
            return;
        }

        var locales = Locales.All;
        for (var i = 0; i < locales.Count; i++)
        {
            var (locale, endonym) = locales[i];
            if (ui.MenuRadioItem(endonym, current == locale))
            {
                Commands.Later(commands, locale, static (a, l) => a.Locale = l);
            }
        }
    }
}

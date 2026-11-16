using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.KeyboardModule;
using ILSight.ExportArea;
using ILSight.CommandArea;
using ILSight.MainArea;
using ILSight.AssemblyArea;
using ILSight.DocumentViewArea;
using ILSight.OmnibarArea;
using static ZGF.Desktop.ShortcutModifiers;

namespace ILSight;

/// <summary>Every key that does something in the app, matched and labelled from here: the app-wide
/// ones <see cref="RunAppShortcuts"/> runs, and those the focused view handles
/// itself. Command is Ctrl, but ⌘ on macOS.</summary>
internal static class Shortcuts
{
    /// <summary>Analyzes what the focused view has picked: the tree's node, the code's reference at
    /// the caret, the analyzer's result.</summary>
    public static readonly Shortcut Analyze = Shortcut.Command(KeyboardKey.R);

    public static readonly Shortcut Open = Shortcut.Command(KeyboardKey.O);
    public static readonly Shortcut SaveCode = Shortcut.Command(KeyboardKey.S);
    public static readonly Shortcut Export = Shortcut.Command(KeyboardKey.E, Shift);
    public static readonly Shortcut Search = Shortcut.Command(KeyboardKey.F, Shift);
    public static readonly Shortcut OmnibarSearch = Shortcut.Command(KeyboardKey.L);
    public static readonly Shortcut ReloadAll = Shortcut.Key(KeyboardKey.F5);
    public static readonly Shortcut CloseTab = Shortcut.Command(KeyboardKey.W);
    public static readonly Shortcut NextTab = Shortcut.Key(KeyboardKey.Tab, Control);
    public static readonly Shortcut PreviousTab = Shortcut.Key(KeyboardKey.Tab, Control | Shift);

    // macOS goes back and forward with ⌘[ and ⌘]: Option and the arrows move by word in a text field.
    public static readonly Shortcut Back = OperatingSystem.IsMacOS()
        ? Shortcut.Command(KeyboardKey.LeftBracket)
        : Shortcut.Key(KeyboardKey.LeftArrow, Alt);
    public static readonly Shortcut Forward = OperatingSystem.IsMacOS()
        ? Shortcut.Command(KeyboardKey.RightBracket)
        : Shortcut.Key(KeyboardKey.RightArrow, Alt);

    public static readonly Shortcut ZoomIn = Shortcut.Command(KeyboardKey.Equals);
    public static readonly Shortcut ZoomOut = Shortcut.Command(KeyboardKey.Minus);
    public static readonly Shortcut ZoomReset = Shortcut.Command(KeyboardKey.Alpha0);

    /// <summary>What zooms the code, the labelled keys first: also Command with Shift and =, which
    /// reads as Command and +, and the keypad's.</summary>
    public static readonly Shortcut[] ZoomInKeys = [ZoomIn, Shortcut.Command(KeyboardKey.Equals, Shift), Shortcut.Command(KeyboardKey.NumpadAdd)];
    public static readonly Shortcut[] ZoomOutKeys = [ZoomOut, Shortcut.Command(KeyboardKey.NumpadSubtract)];
    public static readonly Shortcut[] ZoomResetKeys = [ZoomReset, Shortcut.Command(KeyboardKey.Numpad0)];

    public static readonly Shortcut Copy = Shortcut.Command(KeyboardKey.C);
    public static readonly Shortcut CopyPlainText = Shortcut.Command(KeyboardKey.C, Shift);
    public static readonly Shortcut SelectAll = Shortcut.Command(KeyboardKey.A);
    public static readonly Shortcut Find = Shortcut.Command(KeyboardKey.F);
    public static readonly Shortcut FindNext = Shortcut.Key(KeyboardKey.F3);
    public static readonly Shortcut FindPrevious = Shortcut.Key(KeyboardKey.F3, Shift);

    // The fold chords take Control itself on every platform: ⌘M minimizes the window on macOS.
    public static readonly Shortcut ToggleFold = Shortcut.Key(KeyboardKey.M, Control).Then(KeyboardKey.M);
    public static readonly Shortcut ToggleAllFolds = Shortcut.Key(KeyboardKey.M, Control).Then(KeyboardKey.L);
    public static readonly Shortcut CollapseFold = Shortcut.Command(KeyboardKey.LeftBracket, Shift);
    public static readonly Shortcut ExpandFold = Shortcut.Command(KeyboardKey.RightBracket, Shift);

    public static readonly Shortcut GoToToken = Shortcut.Command(KeyboardKey.G);
    public static readonly Shortcut Rename = Shortcut.Key(KeyboardKey.F2);
    public static readonly Shortcut Delete = Shortcut.Key(KeyboardKey.Delete);
    public static readonly Shortcut Enter = Shortcut.Key(KeyboardKey.Enter);
    public static readonly Shortcut Space = Shortcut.Key(KeyboardKey.Space);
    public static readonly Shortcut ContextMenu = Shortcut.Key(KeyboardKey.F10, Shift);
    public static readonly Shortcut CloseWindow = Shortcut.Key(KeyboardKey.F4, Alt);

    /// <summary>Opens the display options, as the toolbar's gear does, and Settings in macOS's app menu.</summary>
    public static readonly Shortcut Settings = Shortcut.Command(KeyboardKey.Comma);

    // macOS's app and Window menus, which the system handles.
    public static readonly Shortcut Hide = Shortcut.Command(KeyboardKey.H);
    public static readonly Shortcut HideOthers = Shortcut.Command(KeyboardKey.H, Alt);
    public static readonly Shortcut Quit = Shortcut.Command(KeyboardKey.Q);
    public static readonly Shortcut Minimize = Shortcut.Command(KeyboardKey.M);

    private static void GoBack(IAppCommandWriter commands, AppState appState)
    {
        Commands.MoveLater(commands, appState.ListSession.Navigation.GoBack(appState.ListSession.Assemblies, appState.FinishedExports, appState.Decompiler.Setup));
    }

    private static void GoForward(IAppCommandWriter commands, AppState appState)
    {
        Commands.MoveLater(commands, appState.ListSession.Navigation.GoForward(appState.ListSession.Assemblies, appState.FinishedExports, appState.Decompiler.Setup));
    }

    /// <summary>Runs the app-wide shortcuts pressed this frame: those that run while a text box takes the keys first
    /// (<see cref="RunShortcutsWhileTyping"/>), then, unless a text box takes them, those that act on a view's selection or a
    /// document (<see cref="RunShortcutsOutsideTextBoxes"/>).</summary>
    public static void RunAppShortcuts(Ui ui, Appearance appearance, IAppCommandWriter commands, Panels panels, ScreenDisplay display, Omnibar omnibar,
        BrowseTree tree, ExportSetup exportSetup, AppState appState)
    {
        RunShortcutsWhileTyping(ui, commands, panels, display, omnibar, appState);
        if (!ui.WantsTextInput)
        {
            RunShortcutsOutsideTextBoxes(ui, appearance, commands, panels, tree, exportSetup, appState);
        }
    }

    // The shortcuts that run while a text box takes the keys, which they don't edit text with: the pointer's side
    // buttons going back and forward, and the keys that open a search or a panel or move in the history.
    private static void RunShortcutsWhileTyping(Ui ui, IAppCommandWriter commands, Panels panels, ScreenDisplay display, Omnibar omnibar, AppState appState)
    {
        var input = ui.Input;
        if (input.WasPressed(PointerButton.Back))
        {
            GoBack(commands, appState);
        }

        if (input.WasPressed(PointerButton.Forward))
        {
            GoForward(commands, appState);
        }

        if (ui.IsPressed(Search))
        {
            panels.OpenSearch();
        }

        if (ui.IsPressed(OmnibarSearch))
        {
            SearchInOmnibarUnlessFolding(ui, panels, display, omnibar);
        }

        if (ui.IsPressed(Back))
        {
            GoBack(commands, appState);
        }

        if (ui.IsPressed(Forward))
        {
            GoForward(commands, appState);
        }

        if (ui.IsPressed(Settings))
        {
            panels.Open(DockView.DisplayOptions);
        }
    }

    // The shortcuts that act on a view's selection or a document, run only while no text box takes the keys, where the
    // same keys edit the text.
    private static void RunShortcutsOutsideTextBoxes(Ui ui, Appearance appearance, IAppCommandWriter commands, Panels panels, BrowseTree tree,
        ExportSetup exportSetup, AppState appState)
    {
        var navigation = appState.ListSession.Navigation;
        var setup = appState.Decompiler.Setup;
        if (ui.IsPressed(Open))
        {
            MainMenuView.OpenFile(ui, appearance, commands, navigation);
        }

        if (ui.IsPressed(SaveCode))
        {
            SaveDialogs.PickActiveCodeFile(ui, appearance, commands, navigation, setup);
        }

        if (ui.IsPressed(Export))
        {
            exportSetup.Open(appState.ListSession.Assemblies.Assemblies, Exports.FindActiveAssembly(navigation));
        }

        if (ui.IsPressed(ReloadAll))
        {
            Commands.ReloadLater(commands, tree.ListExpandedPaths());
        }

        if (ui.IsPressed(CloseTab))
        {
            panels.CloseActiveTab();
        }

        if (ui.IsPressed(NextTab))
        {
            panels.CycleTab(navigation, setup, 1);
        }

        if (ui.IsPressed(PreviousTab))
        {
            panels.CycleTab(navigation, setup, -1);
        }
    }

    private static void SearchInOmnibarUnlessFolding(Ui ui, Panels panels, ScreenDisplay display, Omnibar omnibar)
    {
        // The key also ends Ctrl+M, Ctrl+L, which folds the code instead.
        if (ui.IsPressed(ToggleAllFolds))
        {
            return;
        }

        ScreenLayout.SearchInOmnibar(panels, display, omnibar);
    }

    /// <summary>Whether any of <paramref name="shortcuts"/> was pressed this frame.</summary>
    public static bool AnyPressed(Ui ui, Shortcut[] shortcuts)
    {
        foreach (var shortcut in shortcuts)
        {
            if (ui.IsPressed(shortcut))
            {
                return true;
            }
        }
        return false;
    }
}

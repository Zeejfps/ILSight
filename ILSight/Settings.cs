using ZGF.Gui.Immediate;
using System.Diagnostics;
using System.Globalization;
using ZGF.Desktop;
using ILSight.Core;
using ILSight.Localization;
using static ILSight.AssemblyLists;
using static ILSight.Bookmarks;
using static ILSight.IconTheming;
using static ILSight.SavedDocuments;
using static ILSight.Theming;

namespace ILSight;

/// <summary>Saving the settings when something in them changed.</summary>
public static class Settings
{
    private static readonly Saving.Unchanged Unchanged = new();

    /// <summary>The app state <paramref name="settings"/> describe, with the open list's documents
    /// reopened, what is saved recorded as saved, and the user's themes and icon themes read.</summary>
    public static AppState LoadAppState(AppSettings settings, BrowseSession session)
    {
        var appState = CreateAppState(settings, session);
        var first = Tabs.AddNew(appState.Tabs);
        Tabs.MakeActive(appState.Tabs, first);
        LoadLists(appState.Lists, settings.AssemblyLists, settings.ActiveAssemblyList);
        var leftOutBookmarks = LoadBookmarks(appState.Bookmarks, settings.Bookmarks);
        Analysis.AddPending(appState.Analyzer, appState.Lists.ActiveAnalyses);
        ReopenDocuments(appState);
        appState.LastSaved = CaptureSavedSources(appState);
        ReportLeftOutBookmarks(appState, leftOutBookmarks);
        LoadThemes(appState);
        LoadIconThemes(appState);
        return appState;
    }

    /// <summary>The app state <paramref name="settings"/> describe, before anything is reopened or read
    /// from disk; <see cref="LoadAppState"/> does that. The tooltips report to the error log rather than to
    /// the app state, which they are part of.</summary>
    private static AppState CreateAppState(AppSettings settings, BrowseSession session)
    {
        var assemblies = new AssemblyList();
        var errors = new ErrorLog();
        var work = new BackgroundWork();
        var appState = new AppState
        {
            Work = work,
            Session = session,
            SettingsPlace = new SettingsPlace(settings.FilePath),
            View = ReadViewSettings(settings),
            Window = settings.Window,
            Assemblies = assemblies,
            Errors = errors,
            Tooltips = new CodeTooltips { Assemblies = assemblies, Work = work, ReportError = problem => Errors.Report(errors, problem), Version = assemblies.Version },
            Theme = settings.Theme,
            IconTheme = settings.IconTheme,
            Language = settings.Language,
            DecompilerOptions = settings.Decompiler,
            Locale = ChooseLocale(settings),
            ApiVisibility = settings.ApiVisibility,
            Updates = new AppUpdates { Feed = GitHubUpdates.CreateFeed(), CheckAutomatically = settings.CheckForUpdates },
        };
        AssemblyListing.UseLoadOptions(assemblies, settings.Decompiler);
        return appState;
    }

    private static Locale ChooseLocale(AppSettings settings)
    {
        if (settings.Locale != null)
            return settings.Locale.Value;
        return Locales.For(CultureInfo.CurrentUICulture);
    }

    /// <summary>The folder <paramref name="name"/> beside <paramref name="settingsFile"/>; null while the
    /// settings are kept in memory only.</summary>
    public static string? FindFolderBeside(string? settingsFile, string name)
    {
        if (settingsFile == null)
            return null;
        var folder = Path.GetDirectoryName(settingsFile);
        if (folder == null)
            return null;
        return Path.Combine(folder, name);
    }

    /// <summary>Saves the settings when what they are saved from changed since the last save written,
    /// unless the assemblies are reloading or a failed write is still waiting to be tried again at
    /// <paramref name="now"/>.</summary>
    public static Saving SaveSettingsIfChanged(AppState appState, double now)
    {
        if (appState.IsReloading)
            return Unchanged;
        var sources = CaptureSavedSources(appState);
        if (sources == appState.LastSaved)
            return Unchanged;
        var wait = AdvanceRetry(appState, now);
        if (wait > 0)
            return new Saving.Waiting(wait);
        return SaveSettings(appState) switch
        {
            SettingsWriteResult.Written => new Saving.Saved(),
            SettingsWriteResult.Failed failed => new Saving.Failed(failed.Reason, AdvanceRetry(appState, now)),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>Moves the wait on, starting it if this is the first time it is asked for after a failure,
    /// and returns the seconds left at <paramref name="now"/> before a failed write is tried again.</summary>
    private static double AdvanceRetry(AppState appState, double now)
    {
        switch (appState.SaveRetry)
        {
            case SaveRetry.Clear:
                return 0;
            case SaveRetry.Failed:
                appState.SaveRetry = new SaveRetry.Waiting(now);
                return SaveRetry.DelaySeconds;
            case SaveRetry.Waiting waiting:
                return Math.Max(0, SaveRetry.DelaySeconds - (now - waiting.Since));
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Tells the user what was wrong with the settings file, if anything.</summary>
    public static void ReportSettingsProblem(AppState appState, SettingsProblem problem)
    {
        if (problem is SettingsProblem.None)
            return;
        Errors.Report(appState, new AppProblem.SettingsUnreadable(appState.SettingsPlace.FilePath, problem));
    }

    /// <summary>Writes the settings, counting what they were copied from as saved only once the write
    /// succeeds. A failed write is reported, unless writes were failing already.</summary>
    public static SettingsWriteResult SaveSettings(AppState appState)
    {
        RememberActiveAssemblyList(appState);
        var sources = CaptureSavedSources(appState);
        var write = SettingsJson.SaveSettings(CreateSettings(appState));
        TakeWrite(appState, sources, write);
        return write;
    }

    internal static SavedSources CaptureSavedSources(AppState appState)
    {
        return new SavedSources(
            appState.Assemblies.Version,
            appState.Lists.Revision,
            appState.Bookmarks.Revision,
            appState.Analyzer.Revision,
            appState.Tabs.Changes,
            appState.ViewSettingsChanges,
            appState.Theme,
            appState.IconTheme,
            appState.Language,
            appState.DecompilerOptions,
            appState.ApiVisibility,
            appState.Locale,
            appState.Updates.CheckAutomatically);
    }

    /// <summary>The settings as the app state and the screen it was handed hold them now, to be written
    /// where they were read from.</summary>
    private static AppSettings CreateSettings(AppState appState)
    {
        var view = appState.View;
        return new AppSettings
        {
            FilePath = appState.SettingsPlace.FilePath,
            AssemblyLists = AssemblyLists.SaveLists(appState.Lists),
            ActiveAssemblyList = appState.Lists.Active,
            Theme = appState.Theme,
            IconTheme = appState.IconTheme,
            Language = appState.Language,
            Decompiler = appState.DecompilerOptions,
            ApiVisibility = appState.ApiVisibility,
            CheckForUpdates = appState.Updates.CheckAutomatically,
            Locale = appState.Locale,
            Bookmarks = ILSight.Bookmarks.ListRecords(appState.Bookmarks),
            Window = appState.Window,
            BookmarkColumns = view.BookmarkColumns,
            SearchColumns = view.SearchColumns,
            ExportColumns = view.ExportColumns,
            FoldedOptionGroups = view.FoldedOptionGroups,
            SearchKinds = view.SearchKinds,
            FlagsFilters = view.FlagsFilters,
            ExportWindow = view.ExportWindow,
            Layout = view.Layout,
            Display = view.Display,
            NuGet = view.NuGet,
            NuGetWindow = view.NuGetWindow,
            ProcessWindow = view.ProcessWindow,
            ProcessColumns = view.ProcessColumns,
            ProcessModuleColumns = view.ProcessModuleColumns,
            CloseWindow = view.CloseWindow,
        };
    }

    private static void TakeWrite(AppState appState, SavedSources sources, SettingsWriteResult write)
    {
        switch (write)
        {
            case SettingsWriteResult.Written:
                appState.LastSaved = sources;
                appState.SaveRetry = new SaveRetry.Clear();
                break;
            case SettingsWriteResult.Failed failed:
                ReportWriteFailure(appState, failed.Reason);
                break;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Reports a failed write, unless writes were failing already.</summary>
    private static void ReportWriteFailure(AppState appState, string error)
    {
        var wasFailing = appState.SaveRetry is not SaveRetry.Clear;
        appState.SaveRetry = new SaveRetry.Failed();
        if (wasFailing)
            return;
        Errors.Report(appState, new AppProblem.SettingsNotSaved(error));
    }

    /// <summary>Takes what the settings keep of the screen, as it changed, to be written with the next save.</summary>
    public static void KeepViewSettings(AppState appState, ViewSettings view)
    {
        appState.View = view;
        appState.ViewSettingsChanges++;
    }

    /// <summary>The main window as it was left, to be written with the next save.</summary>
    public static void KeepWindow(AppState appState, WindowBounds window)
    {
        appState.Window = SettingsJson.SanitizeWindow(window);
    }

    /// <summary>What <paramref name="settings"/> kept of the screen.</summary>
    public static ViewSettings ReadViewSettings(AppSettings settings)
    {
        return new ViewSettings(
            settings.BookmarkColumns,
            settings.SearchColumns,
            settings.ExportColumns,
            settings.FoldedOptionGroups,
            settings.SearchKinds,
            settings.FlagsFilters,
            settings.ExportWindow,
            settings.Layout,
            settings.Display,
            settings.NuGet,
            settings.NuGetWindow,
            settings.ProcessWindow,
            settings.ProcessColumns,
            settings.ProcessModuleColumns,
            settings.CloseWindow,
            Tabs: null);
    }
}

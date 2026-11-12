using System.Diagnostics;
using ZGF.Desktop;
using ILSight.Core;
using static ILSight.AssemblyListKeeping.AssemblyLists;
using static ILSight.Bookmarking.Bookmarks;
using static ILSight.SavedDocuments;
using ILSight.AppSettingsFormat;

namespace ILSight.SettingsKeeping;

/// <summary>Restoring what the settings keep into the app state, and the settings as the app state holds them now, to
/// be saved (<see cref="SettingsSaving"/>).</summary>
public static class Settings
{
    /// <summary>Restores into <paramref name="appState"/> what <paramref name="settings"/> keep: the assembly
    /// lists, the bookmarks and analyses, and the open list's documents reopened; records what is saved as saved.
    /// Returns what to tell the user: how many of the bookmarks were left out because another marks the same line, or
    /// null when none was.</summary>
    public static AppProblem? Restore(AppState appState, AppSettings settings)
    {
        appState.Lists.Restore(settings.AssemblyLists, settings.ActiveAssemblyList);
        var leftOutBookmarks = appState.Bookmarks.Restore(settings.Bookmarks);
        appState.Analyzer.AddPending(appState.Lists.ActiveAnalyses);
        ReopenDocuments(appState.Lists, appState.ListSession);
        appState.LastSaved = CaptureSavedSources(appState);
        if (leftOutBookmarks == 0)
        {
            return null;
        }

        return new AppProblem.BookmarksLeftOut(leftOutBookmarks);
    }

    /// <summary>The folder <paramref name="name"/> beside <paramref name="settingsFile"/>; null while the
    /// settings are kept in memory only.</summary>
    public static string? FindFolderBeside(FullFilePath? settingsFile, string name)
    {
        if (settingsFile == null)
        {
            return null;
        }

        return Path.Combine(settingsFile.Folder, name);
    }

    /// <summary>What to tell the user of what was wrong with the settings file read from
    /// <paramref name="place"/>, or null when nothing was.</summary>
    public static AppProblem? FindProblem(SettingsPlace place, SettingsProblem problem)
    {
        if (problem is SettingsProblem.None)
        {
            return null;
        }

        return new AppProblem.SettingsUnreadable(place.FilePath?.Text, problem);
    }

    internal static SavedSources CaptureSavedSources(AppState appState)
    {
        return new SavedSources(
            appState.Assemblies.Version,
            appState.Lists.Revision,
            appState.Bookmarks.Revision,
            appState.Analyzer.Revision,
            appState.Navigation.TabChanges,
            appState.ViewSettingsChanges,
            appState.Theme,
            appState.IconTheme,
            appState.Language,
            appState.DecompilerOptions,
            appState.ApiVisibility,
            appState.Locale,
            appState.IsUpdateCheckAutomatic);
    }

    /// <summary>The settings as the app state and the screen it was handed hold them now, to be written
    /// where they were read from.</summary>
    internal static AppSettings CreateSettings(AppState appState)
    {
        var view = appState.View;
        return new AppSettings
        {
            FilePath = appState.SettingsPlace.FilePath,
            CanSaveOverFile = appState.SettingsPlace.CanSaveOverFile,
            AssemblyLists = appState.Lists.ToRecords(),
            ActiveAssemblyList = appState.Lists.Active,
            Theme = appState.Theme,
            IconTheme = appState.IconTheme,
            Language = appState.Language,
            Decompiler = appState.DecompilerOptions,
            ApiVisibility = appState.ApiVisibility,
            CheckForUpdates = appState.IsUpdateCheckAutomatic,
            Locale = appState.Locale,
            Bookmarks = appState.Bookmarks.ListRecords(),
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

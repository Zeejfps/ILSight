namespace ILSight.UserArea;

/// <summary>Watches the folders of the user's color and icon themes (<see cref="UserThemes{TFile, TContents, TTheme}.WatchAsync"/>),
/// so a theme redraws as its file is saved.</summary>
[Edge]
public sealed class UserThemeWatching
{
    private readonly WorkflowRunner _workflows;
    private readonly UserThemes<ThemeFile, ThemeContents, AppTheme> _colorThemes;
    private readonly UserThemes<IconThemeFile, IconThemeContents, IconTheme> _iconThemes;

    /// <param name="workflows">Runs the watches.</param>
    /// <param name="colorThemes">The user's color themes.</param>
    /// <param name="iconThemes">The user's icon themes.</param>
    internal UserThemeWatching(WorkflowRunner workflows, UserThemes<ThemeFile, ThemeContents, AppTheme> colorThemes,
        UserThemes<IconThemeFile, IconThemeContents, IconTheme> iconThemes)
    {
        _workflows = workflows;
        _colorThemes = colorThemes;
        _iconThemes = iconThemes;
    }

    /// <summary>Starts watching both folders, until the app shuts down.</summary>
    public void StartWatching()
    {
        _workflows.StartUntilShutdown(_colorThemes.WatchAsync);
        _workflows.StartUntilShutdown(_iconThemes.WatchAsync);
    }
}

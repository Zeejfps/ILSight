namespace ILSight.UserArea;

/// <summary>A kind of theme file the user keeps (<see cref="UserThemes{TFile, TContents, TTheme}"/>): what a new one
/// of the kind says, which the kind writes as its own reader reads it, whoever asks for one.</summary>
public interface IUserThemeFile<TSelf, TContents, TTheme> : IThemeFile<TSelf, TContents>
    where TSelf : IUserThemeFile<TSelf, TContents, TTheme>
    where TContents : class
{
    /// <summary>The text of a new theme named <paramref name="name"/> that extends <paramref name="parent"/>, whose
    /// theme is <paramref name="shown"/>.</summary>
    static abstract string ComposeDraft(string name, ThemeId<TSelf> parent, TTheme shown);
}

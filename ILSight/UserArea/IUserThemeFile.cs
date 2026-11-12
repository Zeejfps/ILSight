namespace ILSight.UserArea;

/// <summary>A kind of theme file the user keeps (<see cref="UserThemes{TFile, TContents, TTheme}"/>): how a file of the
/// kind is read, in two steps, since what else it reads is what its text names (<see cref="Parse"/>, then
/// <see cref="Complete"/> with those files read), and what a new one of the kind says, which the kind writes as its own
/// reader reads it, whoever asks for one.</summary>
public interface IUserThemeFile<TSelf, TContents, TTheme> : IThemeFile<TSelf, TContents>
    where TSelf : IUserThemeFile<TSelf, TContents, TTheme>
    where TContents : class
{
    /// <summary>What the theme <paramref name="id"/> says in <paramref name="json"/>, the content of
    /// <paramref name="file"/>, with what is wrong in it; the files it names (<see cref="NamedFiles"/>) are not read
    /// yet.</summary>
    static abstract TSelf Parse(ThemeId<TSelf> id, string file, byte[] json);

    /// <summary><paramref name="parsed"/> finished with what reading the files it names came to,
    /// <paramref name="named"/> by the path the theme writes; one missing there is unreadable.</summary>
    static abstract TSelf Complete(TSelf parsed, IReadOnlyDictionary<string, IconAsset> named);

    /// <summary>The theme <paramref name="id"/> of <paramref name="file"/>, which could not be read for
    /// <paramref name="reason"/>: it has no contents.</summary>
    static abstract TSelf CreateUnreadable(ThemeId<TSelf> id, string file, string reason);

    /// <summary>The text of a new theme named <paramref name="name"/> that extends <paramref name="parent"/>, whose
    /// theme is <paramref name="shown"/>.</summary>
    static abstract string ComposeDraft(string name, ThemeId<TSelf> parent, TTheme shown);

    /// <summary>The files the theme names, each with the line naming it, for its reader to read before it is
    /// completed; none for a kind that names none.</summary>
    IReadOnlyList<ThemeNamedFile> NamedFiles { get; }
}

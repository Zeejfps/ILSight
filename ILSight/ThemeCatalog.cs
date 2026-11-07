using System.Globalization;

namespace ILSight;

/// <summary>The themes of one kind, color or icon, by id: the built-in ones, then the user's. Each is
/// resolved over the themes it extends once, when the catalog loads; one that cannot be resolved falls
/// back to its parent, else to the built-in theme <see cref="ThemeCatalog{TFile, TContents, TTheme}.FindFallbackId"/> names. Keeps what was wrong
/// in each file.</summary>
public sealed class ThemeCatalog<TFile, TContents, TTheme>
    where TFile : class, IThemeFile<TFile, TContents>
    where TContents : class
    where TTheme : class
{
    /// <summary>The built-in themes in the order a menu lists them, which must include <see cref="ThemeCatalog{TFile, TContents, TTheme}.DefaultId"/>.</summary>
    public required IReadOnlyList<TFile> BuiltIns { get; init; }

    /// <summary>The theme <c>file</c> and the themes it extends, <c>chain</c>, each with what it sets, the one extending
    /// no other first, make; or null after reporting why they make none.</summary>
    public required Func<ThemeCatalog<TFile, TContents, TTheme>, TFile, IReadOnlyList<(TFile File, TContents Contents)>, TTheme?> Build { get; init; }

    /// <summary>Gives the built-in theme a file shows when neither it nor its parent resolves.</summary>
    public required Func<TFile, ThemeId<TFile>> FindFallbackId { get; init; }

    public readonly Dictionary<ThemeId<TFile>, TFile> Files = new();
    public readonly Dictionary<ThemeId<TFile>, TTheme> Themes = new();
    public readonly Dictionary<ThemeId<TFile>, ThemeId<TFile>> ShownIds = new();
    public readonly HashSet<ThemeId<TFile>> Resolving = new();
    /// <summary>Every theme's id, built-in ones first, in the order a menu lists them.</summary>
    public readonly List<ThemeId<TFile>> Ids = new();
    /// <summary>What was wrong in the theme files the last load read, each told once.</summary>
    public readonly List<ThemeError> Errors = new();
    public CultureInfo? NamesCulture;
    public string[] Names = [];

    /// <summary>The built-in theme an id no theme has shows.</summary>
    public required ThemeId<TFile> DefaultId { get; init; }

    /// <summary>Changes each time the themes are loaded, so a menu of them knows its items may have moved.</summary>
    public int Revision { get; set; }
}

/// <summary>Loading themes, resolving each over the ones it extends, and finding them.</summary>
public static class ThemeCatalogs
{
    private const string ChainArrow = " → ";

    /// <summary>Takes <paramref name="userThemes"/> in place of the user themes held before, and resolves
    /// every theme again. A user theme whose id is taken is left out.</summary>
    public static void LoadUserThemes<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, IReadOnlyList<TFile> userThemes)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        catalog.Revision++;
        ClearCatalog(catalog);
        foreach (var file in catalog.BuiltIns)
            AddThemeFile(catalog, file);
        foreach (var file in userThemes)
            AddUserThemeFile(catalog, file);
        foreach (var id in catalog.Ids)
            ResolveTheme(catalog, id);
    }

    private static void ClearCatalog<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        catalog.Files.Clear();
        catalog.Themes.Clear();
        catalog.ShownIds.Clear();
        catalog.Ids.Clear();
        catalog.Errors.Clear();
        catalog.NamesCulture = null;
    }

    private static void AddUserThemeFile<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, TFile file)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        if (catalog.Files.ContainsKey(file.Id))
        {
            AddCatalogError(catalog, new ThemeError(file.File, 0, ThemeProblem.IdTaken, file.Id.Text));
            return;
        }
        AddThemeFile(catalog, file);
    }

    private static void AddThemeFile<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, TFile file)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        catalog.Files[file.Id] = file;
        catalog.Ids.Add(file.Id);
        foreach (var error in file.Errors)
            AddCatalogError(catalog, error);
    }

    /// <summary>The theme <paramref name="id"/>, which must be one of <see cref="ThemeCatalog{TFile, TContents, TTheme}.Ids"/>, resolved the
    /// first time it is asked for.</summary>
    public static TTheme ResolveTheme<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, ThemeId<TFile> id)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        if (catalog.Themes.TryGetValue(id, out var resolved))
            return resolved;
        catalog.Resolving.Add(id);
        var file = catalog.Files[id];
        var theme = BuildChain(catalog, file);
        if (theme != null)
            catalog.ShownIds[id] = id;
        else
            theme = FallBackFrom(catalog, file);
        catalog.Resolving.Remove(id);
        catalog.Themes[id] = theme;
        return theme;
    }

    private static TTheme? BuildChain<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, TFile file)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        var chain = ListThemeChain(catalog, file);
        if (chain == null)
            return null;
        return catalog.Build(catalog, file, chain);
    }

    /// <summary><paramref name="file"/> and the themes it extends, each with what it sets, the one extending
    /// no other first; null when one could not be read or the chain breaks off or goes in a circle.</summary>
    private static List<(TFile File, TContents Contents)>? ListThemeChain<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, TFile file)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        var files = ListThemeChainFiles(catalog, file);
        if (files == null)
            return null;
        var chain = new List<(TFile, TContents)>(files.Count);
        foreach (var link in files)
        {
            var contents = link.Contents;
            if (contents == null)
                return null;
            chain.Add((link, contents));
        }
        return chain;
    }

    /// <summary><paramref name="file"/> and the themes it extends, the one extending no other first; null
    /// when the chain breaks off or goes in a circle.</summary>
    private static List<TFile>? ListThemeChainFiles<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, TFile file)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        var chain = new List<TFile> { file };
        var extends = file.Extends;
        var current = file;
        while (extends != null)
        {
            var parent = FindParent(catalog, current, extends, chain);
            if (parent == null)
                return null;
            chain.Insert(0, parent);
            current = parent;
            extends = parent.Extends;
        }
        return chain;
    }

    /// <summary>The theme <paramref name="child"/> extends as <paramref name="extends"/> says, or null after
    /// reporting that there is none or that it is already in <paramref name="chain"/>.</summary>
    private static TFile? FindParent<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, TFile child, ThemeParent<TFile> extends, List<TFile> chain)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        if (!catalog.Files.TryGetValue(extends.Id, out var parent))
        {
            AddCatalogError(catalog, new ThemeError(child.File, extends.Line, ThemeProblem.UnknownParent, extends.Id.Text));
            return null;
        }
        if (chain.Contains(parent))
        {
            AddCatalogError(catalog, new ThemeError(child.File, extends.Line, ThemeProblem.ExtendsCycle, Detail: DescribeCycle<TFile, TContents, TTheme>(chain, parent)));
            return null;
        }
        return parent;
    }

    /// <summary>The circle <paramref name="chain"/> closes by extending <paramref name="parent"/>, from
    /// the theme being resolved on: <c>a → b → a</c>.</summary>
    private static string DescribeCycle<TFile, TContents, TTheme>(List<TFile> chain, TFile parent)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        var ids = chain
            .Select(static f => f.Id.Text)
            .Reverse()
            .Append(parent.Id.Text);
        return string.Join(ChainArrow, ids);
    }

    /// <summary>What <paramref name="file"/> shows when it cannot be resolved: its parent as that resolves,
    /// else the built-in theme <see cref="ThemeCatalog{TFile, TContents, TTheme}.FindFallbackId"/> names.</summary>
    private static TTheme FallBackFrom<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, TFile file)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        var target = FindFallBack(catalog, file);
        var theme = ResolveTheme(catalog, target);
        catalog.ShownIds[file.Id] = catalog.ShownIds[target];
        return theme;
    }

    private static ThemeId<TFile> FindFallBack<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, TFile file)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        var extends = file.Extends;
        if (extends != null && CanFallBackTo(catalog, extends.Id))
            return extends.Id;
        var builtIn = catalog.FindFallbackId(file);
        if (catalog.Resolving.Contains(builtIn))
            throw new InvalidOperationException($"The built-in theme '{builtIn}' cannot be resolved.");
        return builtIn;
    }

    private static bool CanFallBackTo<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, ThemeId<TFile> parent)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        if (!catalog.Files.ContainsKey(parent))
            return false;
        return !catalog.Resolving.Contains(parent);
    }

    /// <summary>What the built-in theme <paramref name="id"/> sets; throws when it could not be read, as a
    /// built-in theme always can.</summary>
    public static TContents GetBuiltInContents<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, ThemeId<TFile> id)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        var contents = catalog.Files[id].Contents;
        if (contents == null)
            throw new InvalidOperationException($"The built-in theme '{id}' cannot be read.");
        return contents;
    }

    public static void AddCatalogErrors<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, IEnumerable<ThemeError> errors)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        foreach (var error in errors)
            AddCatalogError(catalog, error);
    }

    public static void AddCatalogError<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, ThemeError error)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        if (catalog.Errors.Contains(error))
            return;
        catalog.Errors.Add(error);
    }

    /// <summary>The theme <paramref name="id"/> as resolved, or the built-in <see cref="ThemeCatalog{TFile, TContents, TTheme}.DefaultId"/> when
    /// there is none with that id.</summary>
    public static TTheme FindTheme<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, ThemeId<TFile> id)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        if (catalog.Themes.TryGetValue(id, out var theme))
            return theme;
        return catalog.Themes[catalog.DefaultId];
    }

    /// <summary>The id of the theme <see cref="FindTheme"/> shows for <paramref name="id"/>: the id itself when
    /// it resolves, else the theme it falls back to.</summary>
    public static ThemeId<TFile> FindShownId<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, ThemeId<TFile> id)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        if (catalog.ShownIds.TryGetValue(id, out var shown))
            return shown;
        return catalog.ShownIds[catalog.DefaultId];
    }

    /// <summary>The name of the theme <paramref name="id"/> in <paramref name="culture"/>'s language.</summary>
    public static string GetThemeName<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, ThemeId<TFile> id, CultureInfo culture)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        return ThemeNames.GetName(catalog.Files[id].Name, culture);
    }

    /// <summary>Where <paramref name="id"/> is in <see cref="ThemeCatalog{TFile, TContents, TTheme}.Ids"/>, or -1. Allocates nothing.</summary>
    public static int FindThemeIndex<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, ThemeId<TFile> id)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        for (var i = 0; i < catalog.Ids.Count; i++)
        {
            if (catalog.Ids[i].Equals(id))
                return i;
        }
        return -1;
    }

    /// <summary>Every theme's name in <paramref name="culture"/>'s language, in the order of
    /// <see cref="ThemeCatalog{TFile, TContents, TTheme}.Ids"/>. Kept until the language or the themes change.</summary>
    public static string[] ListNames<TFile, TContents, TTheme>(ThemeCatalog<TFile, TContents, TTheme> catalog, CultureInfo culture)
        where TFile : class, IThemeFile<TFile, TContents>
        where TContents : class
        where TTheme : class
    {
        if (culture.Equals(catalog.NamesCulture))
            return catalog.Names;
        catalog.Names = catalog.Ids
            .Select(id => ThemeNames.GetName(catalog.Files[id].Name, culture))
            .ToArray();
        catalog.NamesCulture = culture;
        return catalog.Names;
    }
}

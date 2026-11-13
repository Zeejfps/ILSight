using System.Globalization;

namespace ILSight;

/// <summary>The themes of one kind, color or icon, by id: the built-in ones, then the user's. Each is
/// resolved over the themes it extends once, when the catalog loads; one that cannot be resolved falls
/// back to its parent, else to the built-in theme <see cref="FindFallbackId"/> names. Keeps what was wrong
/// in each file.</summary>
public sealed class ThemeCatalog<TFile, TContents, TTheme>
    where TFile : class, IThemeFile<TFile, TContents>
    where TContents : class
    where TTheme : class
{
    private const string ChainArrow = " → ";

    private readonly Dictionary<ThemeId<TFile>, TFile> _files = new();
    private readonly Dictionary<ThemeId<TFile>, TTheme> _themes = new();
    private readonly Dictionary<ThemeId<TFile>, ThemeId<TFile>> _shownIds = new();
    private readonly HashSet<ThemeId<TFile>> _resolving = new();
    private readonly List<ThemeId<TFile>> _ids = new();
    private readonly List<ThemeError> _errors = new();
    private CultureInfo? _namesCulture;
    private string[] _names = [];

    /// <summary>The built-in themes in the order a menu lists them, which must include <see cref="DefaultId"/>.</summary>
    public required IReadOnlyList<TFile> BuiltIns { get; init; }

    /// <summary>The theme <c>file</c> and the themes it extends, <c>chain</c>, each with what it sets, the one extending
    /// no other first, make; or null after reporting why they make none.</summary>
    public required Func<ThemeCatalog<TFile, TContents, TTheme>, TFile, IReadOnlyList<(TFile File, TContents Contents)>, TTheme?> Build { get; init; }

    /// <summary>Gives the built-in theme a file shows when neither it nor its parent resolves.</summary>
    public required Func<TFile, ThemeId<TFile>> FindFallbackId { get; init; }

    /// <summary>The built-in theme an id no theme has shows.</summary>
    public required ThemeId<TFile> DefaultId { get; init; }

    /// <summary>Every theme's id, built-in ones first, in the order a menu lists them.</summary>
    public IReadOnlyList<ThemeId<TFile>> Ids => _ids;

    /// <summary>What was wrong in the theme files the last load read, each told once.</summary>
    public IReadOnlyList<ThemeError> Errors => _errors;

    /// <summary>Changes each time the themes are loaded, so a menu of them knows its items may have moved.</summary>
    public int Revision { get; private set; }

    /// <summary>Takes <paramref name="userThemes"/> in place of the user themes held before, and resolves
    /// every theme again. A user theme whose id is taken is left out.</summary>
    public void ReplaceUserThemes(IReadOnlyList<TFile> userThemes)
    {
        Revision++;
        Clear();
        foreach (var file in BuiltIns)
        {
            AddThemeFile(file);
        }

        foreach (var file in userThemes)
        {
            AddUserThemeFile(file);
        }

        foreach (var id in _ids)
        {
            ResolveTheme(id);
        }
    }

    private void Clear()
    {
        _files.Clear();
        _themes.Clear();
        _shownIds.Clear();
        _ids.Clear();
        _errors.Clear();
        _namesCulture = null;
    }

    private void AddUserThemeFile(TFile file)
    {
        if (_files.ContainsKey(file.Id))
        {
            AddError(new ThemeError
            {
                File = file.File,
                Line = 0,
                Problem = ThemeProblem.IdTaken,
                Subject = file.Id.Text,
            });
            return;
        }
        AddThemeFile(file);
    }

    private void AddThemeFile(TFile file)
    {
        _files[file.Id] = file;
        _ids.Add(file.Id);
        foreach (var error in file.Errors)
        {
            AddError(error);
        }
    }

    /// <summary>The theme <paramref name="id"/>, which must be one of <see cref="Ids"/>, resolved the
    /// first time it is asked for.</summary>
    public TTheme ResolveTheme(ThemeId<TFile> id)
    {
        if (_themes.TryGetValue(id, out var resolved))
        {
            return resolved;
        }

        _resolving.Add(id);
        var file = _files[id];
        var theme = BuildChain(file);
        if (theme is not null)
        {
            _shownIds[id] = id;
        }
        else
        {
            theme = FallBackFrom(file);
        }

        _resolving.Remove(id);
        _themes[id] = theme;
        return theme;
    }

    private TTheme? BuildChain(TFile file)
    {
        var chain = ListThemeChain(file);
        if (chain is null)
        {
            return null;
        }

        return Build(this, file, chain);
    }

    private List<(TFile File, TContents Contents)>? ListThemeChain(TFile file)
    {
        var files = ListThemeChainFiles(file);
        if (files is null)
        {
            return null;
        }

        var chain = new List<(TFile, TContents)>(files.Count);
        foreach (var link in files)
        {
            var contents = link.Contents;
            if (contents is null)
            {
                return null;
            }

            chain.Add((link, contents));
        }
        return chain;
    }

    private List<TFile>? ListThemeChainFiles(TFile file)
    {
        var chain = new List<TFile> { file };
        var extends = file.Extends;
        var current = file;
        while (extends is not null)
        {
            var parent = FindParent(current, extends, chain);
            if (parent is null)
            {
                return null;
            }

            chain.Insert(0, parent);
            current = parent;
            extends = parent.Extends;
        }
        return chain;
    }

    private TFile? FindParent(TFile child, ThemeParent<TFile> extends, List<TFile> chain)
    {
        if (!_files.TryGetValue(extends.Id, out var parent))
        {
            AddError(new ThemeError
            {
                File = child.File,
                Line = extends.Line,
                Problem = ThemeProblem.UnknownParent,
                Subject = extends.Id.Text,
            });
            return null;
        }
        if (chain.Contains(parent))
        {
            AddError(new ThemeError
            {
                File = child.File,
                Line = extends.Line,
                Problem = ThemeProblem.ExtendsCycle,
                Detail = DescribeCycle(chain, parent),
            });
            return null;
        }
        return parent;
    }

    // From the theme being resolved on: a → b → a.
    private static string DescribeCycle(List<TFile> chain, TFile parent)
    {
        var ids = chain
            .Select(static f => f.Id.Text)
            .Reverse()
            .Append(parent.Id.Text);
        return string.Join(ChainArrow, ids);
    }

    private TTheme FallBackFrom(TFile file)
    {
        var target = GetFallBack(file);
        var theme = ResolveTheme(target);
        _shownIds[file.Id] = _shownIds[target];
        return theme;
    }

    private ThemeId<TFile> GetFallBack(TFile file)
    {
        var extends = file.Extends;
        if (extends is not null && CanFallBackTo(extends.Id))
        {
            return extends.Id;
        }

        var builtIn = FindFallbackId(file);
        if (_resolving.Contains(builtIn))
        {
            throw new InvalidOperationException($"The built-in theme '{builtIn}' cannot be resolved.");
        }

        return builtIn;
    }

    private bool CanFallBackTo(ThemeId<TFile> parent)
    {
        if (!_files.ContainsKey(parent))
        {
            return false;
        }

        return !_resolving.Contains(parent);
    }

    /// <summary>What the built-in theme <paramref name="id"/> sets; throws when it could not be read, as a
    /// built-in theme always can.</summary>
    public TContents GetBuiltInContents(ThemeId<TFile> id)
    {
        var contents = _files[id].Contents;
        if (contents is null)
        {
            throw new InvalidOperationException($"The built-in theme '{id}' cannot be read.");
        }

        return contents;
    }

    public void AddErrors(IEnumerable<ThemeError> errors)
    {
        foreach (var error in errors)
        {
            AddError(error);
        }
    }

    public void AddError(ThemeError error)
    {
        if (_errors.Contains(error))
        {
            return;
        }

        _errors.Add(error);
    }

    /// <summary>The theme <paramref name="id"/> as resolved, or the built-in <see cref="DefaultId"/> when
    /// there is none with that id.</summary>
    public TTheme GetTheme(ThemeId<TFile> id)
    {
        if (_themes.TryGetValue(id, out var theme))
        {
            return theme;
        }

        return _themes[DefaultId];
    }

    /// <summary>The id of the theme <see cref="GetTheme"/> shows for <paramref name="id"/>: the id itself when
    /// it resolves, else the theme it falls back to.</summary>
    public ThemeId<TFile> GetShownId(ThemeId<TFile> id)
    {
        if (_shownIds.TryGetValue(id, out var shown))
        {
            return shown;
        }

        return _shownIds[DefaultId];
    }

    /// <summary>The name of the theme <paramref name="id"/> in <paramref name="culture"/>'s language.</summary>
    public string GetThemeName(ThemeId<TFile> id, CultureInfo culture)
    {
        return ThemeNames.GetName(_files[id].Name, culture);
    }

    /// <summary>Where <paramref name="id"/> is in <see cref="Ids"/>; null when it is not there. Allocates nothing.</summary>
    public int? FindThemeIndex(ThemeId<TFile> id)
    {
        for (var i = 0; i < _ids.Count; i++)
        {
            if (_ids[i].Equals(id))
            {
                return i;
            }
        }
        return null;
    }

    /// <summary>Every theme's name in <paramref name="culture"/>'s language, in the order of
    /// <see cref="Ids"/>. Kept until the language or the themes change.</summary>
    public string[] ListNames(CultureInfo culture)
    {
        if (culture.Equals(_namesCulture))
        {
            return _names;
        }

        _names = _ids
            .Select(id => ThemeNames.GetName(_files[id].Name, culture))
            .ToArray();
        _namesCulture = culture;
        return _names;
    }
}

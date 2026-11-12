using System.Reflection;
using ZGF.AppUtils;

namespace ILSight.BuiltInArea;

/// <summary>The icon themes that ship inside the app, read once from its embedded icon theme files: the
/// reference <c>lucide</c> theme first, then the others by id.</summary>
public static class BuiltInIconThemes
{
    private const string ResourcePrefix = "IconThemes/";
    private const string SchemaResource = "icon-theme.schema.json";
    private const string GuideResource = "icon-themes-readme.md";

    private static readonly Assembly Resources = typeof(BuiltInIconThemes).Assembly;

    public static IReadOnlyList<IconThemeFile> Files { get; } = ReadFiles();

    private static readonly ThemeCatalog<IconThemeFile, IconThemeContents, IconTheme> Catalog = IconTheming.CreateCatalog(Files);

    /// <summary>The reference theme, which draws every key and which the screen starts with until the app
    /// hands it the chosen icon theme.</summary>
    public static IconTheme Reference { get; } = Catalog.GetTheme(IconTheming.ReferenceId);

    /// <summary>What was wrong in the built-in icon theme files; nothing, unless one was broken by an edit.</summary>
    public static IReadOnlyList<ThemeError> Errors => Catalog.Errors;

    /// <summary>The JSON schema icon theme files name in <c>$schema</c>, for editors to complete and check their keys.</summary>
    public static string ReadSchema()
    {
        return EmbeddedAssets.LoadString(Resources, SchemaResource);
    }

    /// <summary>The README the icon themes folder holds, on how to write an icon theme.</summary>
    public static string ReadGuide()
    {
        return EmbeddedAssets.LoadString(Resources, GuideResource);
    }

    private static List<IconThemeFile> ReadFiles()
    {
        return Resources.GetManifestResourceNames()
            .Where(IsIconThemeResource)
            .Select(ReadFile)
            .OrderBy(static f => !f.Id.Equals(IconTheming.ReferenceId))
            .ThenBy(static f => f.Id.Text, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsIconThemeResource(string name)
    {
        return name.StartsWith(ResourcePrefix, StringComparison.Ordinal);
    }

    private static IconThemeFile ReadFile(string resource)
    {
        var id = ThemeId<IconThemeFile>.Of(Path.GetFileNameWithoutExtension(resource));
        var json = EmbeddedAssets.LoadBytes(Resources, resource);
        var parsed = IconThemeFile.Parse(id, resource, json);
        return IconThemeFile.Complete(parsed, EmbeddedIconAssets.ReadNamed(Resources, parsed.NamedFiles));
    }
}

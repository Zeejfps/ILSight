using ZGF.Gui.Immediate;

namespace ILSight.App.Tests;

/// <summary>Selectors for the parts of the app tests reach, built from the test paths the app records, as a
/// snapshot shows them.</summary>
internal static class Targets
{
    /// <summary>The node at <paramref name="segments"/>, with keys taken as they are, so a key such as
    /// <c>Calculator&lt;T&gt;</c> or <c>C#</c> needs no escaping.</summary>
    public static Selector GetPath(params TestSegment[] segments)
    {
        return new Selector.ByPath(new TestPath(segments));
    }

    /// <summary>The row of the assemblies tree at <paramref name="keys"/>: the labels of it and of every row above it,
    /// from its assembly's.</summary>
    public static Selector GetTreeRow(params string[] keys)
    {
        return GetPath([Name("dock"), Name("assemblies"), Name("tree"), .. keys.Select(Key)]);
    }

    /// <summary>The row of the assemblies tree at <paramref name="keys"/> under the fixtures' namespace.</summary>
    public static Selector GetFixtureRow(params string[] keys)
    {
        return GetTreeRow([AppStateDriver.FixturesAssembly, AppStateDriver.FixturesNamespace, .. keys]);
    }

    /// <summary>The row of the analyzer at <paramref name="keys"/>: the labels of it and of every row above it, an
    /// analysis by its header without the count after it.</summary>
    public static Selector GetAnalyzerRow(params string[] keys)
    {
        return GetPath([Name("dock"), Name("analyzer"), Name("analyzer"), .. keys.Select(Key)]);
    }

    /// <summary>The item <paramref name="name"/> of the menu a tree row's right-click opens.</summary>
    public static Selector GetTreeMenuItem(string name)
    {
        return GetPath(Name("dock"), Name("assemblies"), Name("tree-menu"), Name(name));
    }

    /// <summary>The crumb at <paramref name="depth"/>, from 0 for the assembly, over the document in
    /// <paramref name="panel"/>.</summary>
    public static Selector GetCrumb(string panel, int depth)
    {
        return GetPath(Name("dock"), Name(panel), Key(depth.ToString()), Name("crumb"));
    }

    /// <summary>The arrow after the crumb at <paramref name="depth"/>, which lists that node's children.</summary>
    public static Selector GetCrumbArrow(string panel, int depth)
    {
        return GetPath(Name("dock"), Name(panel), Key(depth.ToString()), Name("children"));
    }

    /// <summary>The child labelled <paramref name="label"/> in the list a crumb's arrow opened.</summary>
    public static Selector GetCrumbChild(string panel, string label)
    {
        return GetPath(Name("dock"), Name(panel), Name("child"), Key(label));
    }

    /// <summary>The suggestion reading <paramref name="text"/> under the omnibar's search box in <paramref name="panel"/>.</summary>
    public static Selector GetSuggestion(string panel, string text)
    {
        return GetPath(Name("dock"), Name(panel), Name("suggestion"), Key(text));
    }

    /// <summary>The tab of the document titled <paramref name="title"/>.</summary>
    public static Selector GetTab(string title)
    {
        return Selector.OfRole(SemanticRole.Tab, title);
    }

    private static TestSegment Name(string name) => new TestSegment.Named(name);

    private static TestSegment Key(string key) => new TestSegment.Keyed(key);
}

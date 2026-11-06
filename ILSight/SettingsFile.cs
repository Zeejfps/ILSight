using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Comparison;
using ILSight.Core.Diagrams;
using ILSight.Core.Metadata;
using ILSight.Localization;

namespace ILSight;

/// <summary>The settings file as it is written: what <see cref="AppSettings"/> keeps, under the names
/// the file has it by. It is only ever written; <see cref="SettingsReader"/> reads a file one field at
/// a time, through the shapes below, which take what a hand-edited or older file can hold.</summary>
internal sealed record SettingsFile(
    List<AssemblyListJson> AssemblyLists,
    string? ActiveAssemblyList,
    string Theme,
    string IconTheme,
    DecompilationLanguage Language,
    DecompilerOptionsJson Decompiler,
    List<string>? FoldedOptionGroups,
    DisplayOptionsJson? Display,
    Locale? Locale,
    ApiVisibility ApiVisibility,
    bool CheckForUpdates,
    SearchKinds SearchKinds,
    DockLayoutJson? Layout,
    List<BookmarkJson> Bookmarks,
    List<float>? BookmarkColumns,
    List<float>? SearchColumns,
    List<float>? ExportColumns,
    Dictionary<string, SavedFilterStateJson?>? FlagsFilters,
    WindowBoundsJson? Window,
    WindowBoundsJson? ExportWindow,
    NuGetSearchJson? NuGet,
    WindowBoundsJson? NuGetWindow)
{
    /// <summary>The one list that settings files kept before lists had names.</summary>
    public const string OldAssemblies = "Assemblies";

    /// <summary>The symbols analyzed before each list kept its own.</summary>
    public const string OldAnalyzed = "Analyzed";

    /// <summary>The one switch settings files kept before <see cref="ApiVisibility"/> had three choices.</summary>
    public const string OldShowNonPublic = "ShowNonPublic";

    public static SettingsFile From(AppSettings settings) => new(
        settings.AssemblyLists.ConvertAll(AssemblyListJson.From),
        settings.ActiveAssemblyList,
        settings.Theme.Text,
        settings.IconTheme.Text,
        settings.Language,
        SettingsJson.Write(settings.Decompiler),
        settings.FoldedOptionGroups,
        settings.Display == null ? null : SettingsJson.Write(settings.Display),
        settings.Locale,
        settings.ApiVisibility,
        settings.CheckForUpdates,
        settings.SearchKinds,
        settings.Layout == null ? null : DockJson.Write(settings.Layout),
        settings.Bookmarks.ConvertAll(SettingsJson.Write),
        settings.BookmarkColumns,
        settings.SearchColumns,
        settings.ExportColumns,
        FiltersOf(settings.FlagsFilters),
        WindowOf(settings.Window),
        WindowOf(settings.ExportWindow),
        settings.NuGet == null ? null : SettingsJson.Write(settings.NuGet),
        WindowOf(settings.NuGetWindow));

    private static Dictionary<string, SavedFilterStateJson?>? FiltersOf(Dictionary<string, SavedFilterState>? filters)
    {
        if (filters == null)
            return null;
        var written = new Dictionary<string, SavedFilterStateJson?>();
        foreach (var (key, filter) in filters)
            written.Add(key, SettingsJson.Write(filter));
        return written;
    }

    private static WindowBoundsJson? WindowOf(WindowBounds? bounds)
    {
        if (bounds == null)
            return null;
        return SettingsJson.Write(bounds.Value);
    }
}

/// <summary>An assembly list as a settings file can hold it. Without a name it is no list.</summary>
internal sealed record AssemblyListJson(
    string? Name,
    List<string?>? Assemblies,
    List<CodeAnchorJson?>? Analyzed,
    Dictionary<string, string?>? Pdbs,
    Dictionary<string, string?>? Frameworks,
    DocumentsJson? Documents)
{
    public static AssemblyListJson From(AssemblyListRecord list) => new(
        list.Name,
        SettingsJson.WritePaths(list.Assemblies),
        list.Analyzed?.Select(static CodeAnchorJson? (a) => SettingsJson.Write(a)).ToList(),
        PicksJsonOf(list.Pdbs),
        PicksJsonOf(list.Frameworks),
        list.Documents == null ? null : DocumentsJson.From(list.Documents));

    /// <summary>The list, without blank paths and picks or documents missing what they show; null
    /// without a name. A document that is not one at all leaves out <paramref name="field"/>.</summary>
    public AssemblyListRecord? Parse(SettingsReader file, string field)
    {
        if (Name == null)
            return null;
        return new AssemblyListRecord(Name, SettingsJson.ListPaths(Assemblies), SettingsJson.ListAnchors(Analyzed),
            PicksOf(Pdbs), PicksOf(Frameworks), Documents?.Parse(file, field));
    }

    private static Dictionary<string, string?>? PicksJsonOf(Dictionary<AssemblyPath, string>? picks) =>
        picks?.ToDictionary(static p => p.Key.Text, static string? (p) => p.Value);

    private static Dictionary<AssemblyPath, string>? PicksOf(Dictionary<string, string?>? picks)
    {
        if (picks == null)
            return null;
        var kept = new Dictionary<AssemblyPath, string>();
        foreach (var (text, pick) in picks)
        {
            var path = AssemblyPath.Parse(text);
            if (path != null && !string.IsNullOrWhiteSpace(pick))
                kept.TryAdd(path, pick);
        }
        return kept;
    }
}

/// <summary>A symbol analyzed, as a settings file can hold it. Without a file it is none.</summary>
internal sealed record CodeAnchorJson(CodeAnchorKind Kind, string? FilePath, int Token, int ILOffset);

/// <summary>A list's open documents as a settings file can hold them, each tab read on its own.</summary>
internal sealed record DocumentsJson(List<JsonElement>? Tabs, string? Active, string? Preview, DockLayoutJson? Layout)
{
    public static DocumentsJson From(DocumentsRecord documents)
    {
        var json = AppSettingsJson.Default.DocumentJson;
        var tabs = documents.Tabs.ConvertAll(t => JsonSerializer.SerializeToElement(DocumentJson.From(t), json));
        var layout = documents.Layout == null ? null : DockJson.Write(documents.Layout);
        return new DocumentsJson(tabs, documents.Active, documents.Preview, layout);
    }

    /// <summary>The documents, without tabs missing what they show, docked as a view, or docked as
    /// a panel another tab was; null without tabs. A tab that is not one at all, or a layout missing
    /// a part or giving two parts one id, leaves out <paramref name="field"/>.</summary>
    public DocumentsRecord? Parse(SettingsReader file, string field)
    {
        if (Tabs == null)
            return null;
        var tabs = Tabs
            .Select(t => file.Parse(t, AppSettingsJson.Default.DocumentJson, field)?.Parse(() => file.LeaveOut(field)))
            .OfType<DocumentRecord>()
            .Where(static t => !DockPanels.IsView(t.Panel))
            .DistinctBy(static t => t.Panel)
            .ToList();
        return new DocumentsRecord(tabs, Active, Preview, ParseLayout(file, field));
    }

    private DockLayout? ParseLayout(SettingsReader file, string field)
    {
        if (Layout == null)
            return null;
        var layout = DockJson.Parse(Layout);
        if (layout != null && !layout.RepeatsAnId())
            return layout;
        file.LeaveOut(field);
        return null;
    }
}

/// <summary>An open tab as a settings file can hold it. Without a panel or what it shows it is none.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(NodeDocumentJson), "node")]
[JsonDerivedType(typeof(DiagramDocumentJson), "diagram")]
[JsonDerivedType(typeof(ComparisonDocumentJson), "comparison")]
internal abstract record DocumentJson(string? Panel, string? Title)
{
    public static DocumentJson From(DocumentRecord document) => document switch
    {
        NodeDocumentRecord node => NodeDocumentJson.From(node),
        DiagramDocumentRecord diagram => DiagramDocumentJson.From(diagram),
        ComparisonDocumentRecord comparison => ComparisonDocumentJson.From(comparison),
        _ => throw new UnreachableException(),
    };

    /// <summary>The tab; <paramref name="leaveOut"/> is told when what it shows cannot be read.</summary>
    public abstract DocumentRecord? Parse(Action leaveOut);
}

internal sealed record NodeDocumentJson(
    string? Panel,
    string? Title,
    string? FilePath,
    SavedPathJson? Path,
    DecompilationLanguage Language,
    CodeViewPositionJson? Position,
    GridPositionJson? GridPosition) : DocumentJson(Panel, Title)
{
    public static NodeDocumentJson From(NodeDocumentRecord node)
    {
        var code = node.View?.Code;
        var grid = node.View?.Grid;
        return new NodeDocumentJson(node.Panel, node.Title, node.Location.FilePath.Text,
            SettingsJson.Write(node.Location), node.Language,
            code == null ? null : SettingsJson.Write(code.Value),
            grid == null ? null : SettingsJson.Write(grid.Value));
    }

    public override DocumentRecord? Parse(Action leaveOut)
    {
        if (Panel == null || FilePath == null)
            return null;
        var file = AssemblyPath.Parse(FilePath);
        if (file == null)
            return null;
        var location = SettingsJson.GetLocation(file, Path, leaveOut);
        if (location == null)
            return null;
        return new NodeDocumentRecord(Panel, Title, location, Language, ViewOf(Position, GridPosition));
    }

    /// <summary>The view's place as the file keeps it: a code view's, else a grid's, else none.</summary>
    private static ViewPosition? ViewOf(CodeViewPositionJson? code, GridPositionJson? grid)
    {
        if (code != null)
            return ViewPosition.OfCode(SettingsJson.Parse(code.Value));
        if (grid != null)
            return ViewPosition.OfGrid(SettingsJson.Parse(grid.Value));
        return null;
    }
}

/// <summary>Where a code view stood, as a settings file can hold it.</summary>
internal readonly record struct CodeViewPositionJson(int TopLine, float ScrollX, TextPositionJson Anchor, TextPositionJson Caret);

/// <summary>A place in a code view's text, as a settings file can hold it.</summary>
internal readonly record struct TextPositionJson(int Line, int Column);

/// <summary>Where a metadata grid stood, as a settings file can hold it.</summary>
internal readonly record struct GridPositionJson(int TopRow, float ScrollX, int? Row, int? Column);

internal sealed record DiagramDocumentJson(
    string? Panel,
    string? Title,
    List<TypeAnchorJson?>? Types,
    List<string?>? HomeAssemblies,
    DiagramViewJson? View) : DocumentJson(Panel, Title)
{
    public static DiagramDocumentJson From(DiagramDocumentRecord diagram) => new(
        diagram.Panel,
        diagram.Title,
        diagram.Types.Select(static TypeAnchorJson? (t) => SettingsJson.Write(t)).ToList(),
        diagram.HomeAssemblies == null ? null : SettingsJson.WritePaths(diagram.HomeAssemblies),
        diagram.View == null ? null : SettingsJson.Write(diagram.View));

    public override DocumentRecord? Parse(Action leaveOut)
    {
        if (Panel == null || Types == null)
            return null;
        var types = Types.OfType<TypeAnchorJson>().Select(static t => SettingsJson.Parse(t)).OfType<TypeAnchor>().ToList();
        var homes = HomeAssemblies == null ? null : SettingsJson.ListPaths(HomeAssemblies);
        return new DiagramDocumentRecord(Panel, Title, types, homes, View == null ? null : SettingsJson.Parse(View));
    }
}

internal sealed record ComparisonDocumentJson(
    string? Panel,
    string? Title,
    string? LeftPath,
    string? RightPath,
    ApiScope Scope,
    ComparisonViewJson? View) : DocumentJson(Panel, Title)
{
    public static ComparisonDocumentJson From(ComparisonDocumentRecord comparison) => new(
        comparison.Panel,
        comparison.Title,
        comparison.LeftPath.Text,
        comparison.RightPath.Text,
        comparison.Scope,
        comparison.View == null ? null : new ComparisonViewJson(comparison.View.OnlyDifferences));

    public override DocumentRecord? Parse(Action leaveOut)
    {
        if (Panel == null || LeftPath == null || RightPath == null)
            return null;
        var left = AssemblyPath.Parse(LeftPath);
        var right = AssemblyPath.Parse(RightPath);
        if (left == null || right == null)
            return null;
        return new ComparisonDocumentRecord(Panel, Title, left, right, Scope, View == null ? null : SettingsJson.Parse(View));
    }
}

/// <summary>How a comparison was viewed, as a settings file can hold it.</summary>
internal sealed record ComparisonViewJson(bool OnlyDifferences);

/// <summary>A place as a settings or bookmarks file can hold it, in the format of <see cref="Version"/>: a
/// step per node down from the root of the listed file. Of another version, or with a step that cannot be
/// read, it is none.</summary>
internal sealed record SavedPathJson(int Version, List<SavedPathStepJson?>? Steps)
{
    public const int CurrentVersion = 1;
}

/// <summary>A step of a place as a settings or bookmarks file can hold it. Without a segment that reads,
/// or a label, it is none.</summary>
internal sealed record SavedPathStepJson(string? Step, string? Label);

/// <summary>A type as a settings file can hold it. Without its assembly or name it is none.</summary>
internal sealed record TypeAnchorJson(string? AssemblyPath, string? FullName);

/// <summary>How a diagram was viewed, as a settings file can hold it.</summary>
internal sealed record DiagramViewJson(float X, float Y, float Zoom, List<CardFoldJson?>? Folds, bool ShowsHiddenMembers);

/// <summary>A card's part folded or opened, as a settings file can hold it. Without its card it is none.</summary>
internal sealed record CardFoldJson(TypeAnchorJson? Card, MemberSection? Section, string? InheritedFrom);

/// <summary>The feed last searched, as a settings file can hold it. Without a feed it is none.</summary>
internal sealed record NuGetSearchJson(string? FeedUrl, bool IncludePrerelease);

/// <summary>A bookmark as this app's settings or the real ILSpy's bookmarks file can hold it.</summary>
internal sealed record BookmarkJson(
    string? Name,
    bool Enabled,
    string? FileName,
    string? AssemblyFullName,
    string? ModuleName,
    uint Token,
    BookmarkKind Kind,
    int ILOffset,
    int LineNumber,
    string? MemberName,
    string? LocationNodeName,
    BookmarkViewStateJson? ViewState,
    DecompilationLanguage? Language,
    SavedPathJson? Path);

/// <summary>ILSpy's view of a bookmark's document, as a settings or bookmarks file can hold it.</summary>
internal sealed record BookmarkViewStateJson(
    int Version,
    int CaretOffset,
    double VerticalOffset,
    double HorizontalOffset,
    int? FoldingChecksum,
    List<BookmarkFoldingRangeJson?>? ExpandedFoldings,
    BookmarkRenderedLayoutSettingsJson? RenderedLayoutSettings,
    List<string?>? SelectedTreeNodePath);

/// <summary>A folded region left open, as a settings or bookmarks file can hold it.</summary>
internal sealed record BookmarkFoldingRangeJson(int Start, int End);

/// <summary>ILSpy's display settings a bookmark was made in, as a settings or bookmarks file can hold them.</summary>
internal sealed record BookmarkRenderedLayoutSettingsJson(
    bool FoldBraces,
    bool ExpandXmlDocumentationComments,
    bool ExpandMemberDefinitions,
    bool ExpandUsingDeclarations,
    bool ShowDebugInfo,
    bool IndentationUseTabs,
    int IndentationSize,
    int IndentationTabSize);

/// <summary>How the app is drawn, as a settings file can hold it. What it lacks, or holds as null, is
/// as <see cref="DisplayOptions.Default"/> has it; a font size out of range is brought within it and the
/// interface scale is the nearest offered.</summary>
internal sealed record DisplayOptionsJson(
    float? FontSize,
    bool? ShowLineNumbers,
    bool? HighlightCurrentLine,
    bool? HighlightMatchingBrackets,
    bool? ShowOmnibar,
    float? UiScale,
    CodeFontJson? Font,
    TokenFormat? TreeTokens,
    bool? ShowEmptyMetadataTables,
    bool? NestedNamespaces,
    bool? SystemTitleBar,
    bool? PreviewTabs);

/// <summary>A font to draw code in, as a settings file can hold it. Without its family or file it is
/// none, and the bundled font stands in.</summary>
internal sealed record CodeFontJson(string? Family, string? Path, int FaceIndex);

/// <summary>The choices that change how C# is decompiled, as a settings file can hold them. What it
/// lacks, or holds as null, is as <see cref="DecompilerOptions.Default"/> has it.</summary>
internal sealed record DecompilerOptionsJson(CSharpVersion? LanguageVersion, Dictionary<string, bool>? Toggles);

/// <summary>Where a window sat and how big it was, as a settings file can hold it.</summary>
internal sealed record WindowBoundsJson(int X, int Y, int Width, int Height, bool Maximized);

/// <summary>A flags filter, as a settings file can hold it. A group without values is left as it starts.</summary>
internal sealed record SavedFilterStateJson(Dictionary<string, uint[]?>? Groups, Dictionary<string, TriState>? Flags, MatchMode Mode);

/// <summary>The real ILSpy's bookmarks file as it can hold them, each bookmark read on its own.</summary>
internal sealed record BookmarkFileJson(int Version, List<JsonElement>? Bookmarks);

/// <summary>What the shapes share.</summary>
internal static class SettingsJson
{
    public static List<string?> WritePaths(List<AssemblyPath> paths)
    {
        return paths.ConvertAll(static string? (p) => p.Text);
    }

    /// <summary>The absolute paths among <paramref name="paths"/>; none when there are none.</summary>
    public static List<AssemblyPath> ListPaths(List<string?>? paths)
    {
        if (paths == null)
            return new List<AssemblyPath>();
        return paths
            .OfType<string>()
            .Select(AssemblyPath.Parse)
            .OfType<AssemblyPath>()
            .ToList();
    }

    /// <summary>The symbols in <paramref name="anchors"/> that name a file; none when there are none.</summary>
    public static List<CodeAnchor> ListAnchors(List<CodeAnchorJson?>? anchors)
    {
        if (anchors == null)
            return new List<CodeAnchor>();
        return anchors.OfType<CodeAnchorJson>().Select(static a => Parse(a)).OfType<CodeAnchor>().ToList();
    }

    /// <summary>Where a tab or bookmark in the file at <paramref name="filePath"/> was, as
    /// <paramref name="path"/> says. Null when there is none, or when it cannot be read, which
    /// <paramref name="leaveOut"/> is told; never the root for a path that cannot be read.</summary>
    public static NodePath? GetLocation(AssemblyPath filePath, SavedPathJson? path, Action leaveOut)
    {
        if (path == null)
            return null;
        var location = Parse(path, filePath);
        if (location == null)
            leaveOut();
        return location;
    }

    /// <summary><paramref name="place"/> as the files keep it.</summary>
    public static SavedPathJson Write(NodePath place)
    {
        return new SavedPathJson(SavedPathJson.CurrentVersion,
            place.Steps.Select(static SavedPathStepJson? (s) => new SavedPathStepJson(SavedPlaces.Write(s.Segment), s.Label)).ToList());
    }

    /// <summary>The path down from the root of the file at <paramref name="filePath"/>; null when it
    /// cannot be read.</summary>
    private static NodePath? Parse(SavedPathJson json, AssemblyPath filePath)
    {
        if (json.Version != SavedPathJson.CurrentVersion || json.Steps == null)
            return null;
        var steps = new List<NodePathStep>(json.Steps.Count);
        foreach (var saved in json.Steps)
        {
            if (saved == null)
                return null;
            var step = Parse(saved);
            if (step == null)
                return null;
            steps.Add(step.Value);
        }
        return NodePath.From(filePath, steps);
    }

    private static NodePathStep? Parse(SavedPathStepJson json)
    {
        if (json.Step == null || json.Label == null)
            return null;
        var segment = SavedPlaces.ParseSegment(json.Step);
        if (segment == null)
            return null;
        return new NodePathStep(segment, json.Label);
    }

    public static BookmarkJson Write(BookmarkRecord bookmark)
    {
        return new BookmarkJson(
            bookmark.Name,
            bookmark.Enabled,
            bookmark.FileName.Text,
            bookmark.AssemblyFullName,
            bookmark.ModuleName,
            bookmark.Token,
            bookmark.Kind,
            bookmark.ILOffset,
            bookmark.LineNumber,
            bookmark.MemberName,
            bookmark.LocationNodeName,
            bookmark.ViewState == null ? null : Write(bookmark.ViewState),
            bookmark.Language,
            bookmark.Location == null ? null : Write(bookmark.Location));
    }

    /// <summary>The bookmark, with what the file lacks filled in; null without the absolute path of an
    /// assembly file. A place that cannot be read is no place, and <paramref name="leaveOutPlace"/> is told.</summary>
    public static BookmarkRecord? Parse(BookmarkJson json, Action leaveOutPlace)
    {
        if (json.FileName == null)
            return null;
        var path = AssemblyPath.Parse(json.FileName);
        if (path == null)
            return null;
        var location = GetLocation(path, json.Path, leaveOutPlace);
        return new BookmarkRecord(json.Name ?? "", json.Enabled, path, json.AssemblyFullName ?? "", json.ModuleName ?? path.FileName,
            json.Token, json.Kind, json.ILOffset, json.LineNumber, json.MemberName ?? "", json.LocationNodeName,
            json.ViewState == null ? null : Parse(json.ViewState), location, json.Language);
    }

    private static BookmarkViewStateJson Write(BookmarkViewState view)
    {
        return new BookmarkViewStateJson(
            view.Version,
            view.CaretOffset,
            view.VerticalOffset,
            view.HorizontalOffset,
            view.FoldingChecksum,
            view.ExpandedFoldings?.Select(static BookmarkFoldingRangeJson? (f) => Write(f)).ToList(),
            view.RenderedLayoutSettings == null ? null : Write(view.RenderedLayoutSettings),
            view.SelectedTreeNodePath == null ? null : new List<string?>(view.SelectedTreeNodePath));
    }

    /// <summary>The view state, without missing foldings; a tree path missing a step is no path.</summary>
    private static BookmarkViewState Parse(BookmarkViewStateJson json)
    {
        var foldings = json.ExpandedFoldings?.OfType<BookmarkFoldingRangeJson>().Select(static f => Parse(f)).ToList();
        return new BookmarkViewState(json.Version, json.CaretOffset, json.VerticalOffset, json.HorizontalOffset, json.FoldingChecksum, foldings,
            json.RenderedLayoutSettings == null ? null : Parse(json.RenderedLayoutSettings), ParseTreePath(json.SelectedTreeNodePath));
    }

    private static List<string>? ParseTreePath(List<string?>? path)
    {
        if (path == null)
            return null;
        var steps = path.OfType<string>().ToList();
        if (steps.Count != path.Count)
            return null;
        return steps;
    }

    private static BookmarkFoldingRangeJson Write(BookmarkFoldingRange range)
    {
        return new BookmarkFoldingRangeJson(range.Start, range.End);
    }

    private static BookmarkFoldingRange Parse(BookmarkFoldingRangeJson json)
    {
        return new BookmarkFoldingRange(json.Start, json.End);
    }

    private static BookmarkRenderedLayoutSettingsJson Write(BookmarkRenderedLayoutSettings settings)
    {
        return new BookmarkRenderedLayoutSettingsJson(
            settings.FoldBraces,
            settings.ExpandXmlDocumentationComments,
            settings.ExpandMemberDefinitions,
            settings.ExpandUsingDeclarations,
            settings.ShowDebugInfo,
            settings.IndentationUseTabs,
            settings.IndentationSize,
            settings.IndentationTabSize);
    }

    private static BookmarkRenderedLayoutSettings Parse(BookmarkRenderedLayoutSettingsJson json)
    {
        return new BookmarkRenderedLayoutSettings(
            json.FoldBraces,
            json.ExpandXmlDocumentationComments,
            json.ExpandMemberDefinitions,
            json.ExpandUsingDeclarations,
            json.ShowDebugInfo,
            json.IndentationUseTabs,
            json.IndentationSize,
            json.IndentationTabSize);
    }

    public static CodeAnchorJson Write(CodeAnchor anchor)
    {
        return new CodeAnchorJson(anchor.Kind, anchor.FilePath.Text, anchor.Token, anchor.ILOffset);
    }

    private static CodeAnchor? Parse(CodeAnchorJson json)
    {
        if (json.FilePath == null)
            return null;
        var path = AssemblyPath.Parse(json.FilePath);
        if (path == null)
            return null;
        return new CodeAnchor(json.Kind, path, json.Token, json.ILOffset);
    }

    public static CodeViewPositionJson Write(CodeViewPosition position)
    {
        return new CodeViewPositionJson(position.TopLine, position.ScrollX, Write(position.Anchor), Write(position.Caret));
    }

    public static CodeViewPosition Parse(CodeViewPositionJson json)
    {
        return new CodeViewPosition(json.TopLine, json.ScrollX, Parse(json.Anchor), Parse(json.Caret));
    }

    private static TextPositionJson Write(TextPosition position)
    {
        return new TextPositionJson(position.Line, position.Column);
    }

    private static TextPosition Parse(TextPositionJson json)
    {
        return new TextPosition(json.Line, json.Column);
    }

    public static GridPositionJson Write(GridPosition position)
    {
        return new GridPositionJson(position.TopRow, position.ScrollX, position.Row, position.Column);
    }

    public static GridPosition Parse(GridPositionJson json)
    {
        return new GridPosition(json.TopRow, json.ScrollX, json.Row, json.Column);
    }

    public static ComparisonViewRecord Parse(ComparisonViewJson json)
    {
        return new ComparisonViewRecord(json.OnlyDifferences);
    }

    public static TypeAnchorJson Write(TypeAnchor anchor)
    {
        return new TypeAnchorJson(anchor.AssemblyPath.Text, anchor.FullName);
    }

    public static TypeAnchor? Parse(TypeAnchorJson json)
    {
        if (json.AssemblyPath == null || json.FullName == null)
            return null;
        var path = AssemblyPath.Parse(json.AssemblyPath);
        if (path == null)
            return null;
        return new TypeAnchor(path, json.FullName);
    }

    public static DiagramViewJson Write(DiagramViewRecord view)
    {
        return new DiagramViewJson(view.X, view.Y, view.Zoom,
            view.Folds?.Select(static CardFoldJson? (f) => Write(f)).ToList(), view.ShowsHiddenMembers);
    }

    public static DiagramViewRecord Parse(DiagramViewJson json)
    {
        var folds = json.Folds?.OfType<CardFoldJson>().Select(static f => Parse(f)).OfType<CardFold>().ToList();
        return new DiagramViewRecord(json.X, json.Y, json.Zoom, folds, json.ShowsHiddenMembers);
    }

    private static CardFoldJson Write(CardFold fold)
    {
        return new CardFoldJson(Write(fold.Card), fold.Section, fold.InheritedFrom);
    }

    private static CardFold? Parse(CardFoldJson json)
    {
        if (json.Card == null)
            return null;
        var card = Parse(json.Card);
        if (card == null)
            return null;
        return new CardFold(card.Value, json.Section, json.InheritedFrom);
    }

    public static NuGetSearchJson Write(SavedNuGetSearch search)
    {
        return new NuGetSearchJson(search.FeedUrl, search.IncludePrerelease);
    }

    public static SavedNuGetSearch? Parse(NuGetSearchJson json)
    {
        if (json.FeedUrl == null)
            return null;
        return new SavedNuGetSearch(json.FeedUrl, json.IncludePrerelease);
    }

    public static DisplayOptionsJson Write(DisplayOptions options)
    {
        return new DisplayOptionsJson(
            options.FontSize?.Points,
            options.ShowLineNumbers,
            options.HighlightCurrentLine,
            options.HighlightMatchingBrackets,
            options.ShowOmnibar,
            options.UiScale,
            options.Font == null ? null : Write(options.Font),
            options.TreeTokens,
            options.ShowEmptyMetadataTables,
            options.NestedNamespaces,
            options.SystemTitleBar,
            options.PreviewTabs);
    }

    public static DisplayOptions Parse(DisplayOptionsJson json)
    {
        var d = DisplayOptions.Default;
        var options = new DisplayOptions(
            null,
            json.ShowLineNumbers ?? d.ShowLineNumbers,
            json.HighlightCurrentLine ?? d.HighlightCurrentLine,
            json.HighlightMatchingBrackets ?? d.HighlightMatchingBrackets,
            json.ShowOmnibar ?? d.ShowOmnibar,
            json.UiScale ?? d.UiScale,
            json.Font == null ? null : Parse(json.Font),
            json.TreeTokens ?? d.TreeTokens,
            json.ShowEmptyMetadataTables ?? d.ShowEmptyMetadataTables,
            json.NestedNamespaces ?? d.NestedNamespaces,
            json.SystemTitleBar ?? d.SystemTitleBar,
            json.PreviewTabs ?? d.PreviewTabs);
        var scaled = CodeDisplay.SnapUiScale(options, options.UiScale);
        if (json.FontSize == null)
            return scaled;
        return CodeDisplay.SetFontSize(scaled, json.FontSize.Value);
    }

    private static CodeFontJson Write(CodeFont font)
    {
        return new CodeFontJson(font.Family, font.Path, font.FaceIndex);
    }

    private static CodeFont? Parse(CodeFontJson json)
    {
        if (json.Family == null || json.Path == null)
            return null;
        return new CodeFont(json.Family, json.Path, json.FaceIndex);
    }

    public static DecompilerOptionsJson Write(DecompilerOptions options)
    {
        return new DecompilerOptionsJson(options.LanguageVersion, options.Changes.ToDictionary(static c => c.Key.Name, static c => c.Value));
    }

    /// <summary>The options, a toggle at its default taken as not set; a toggle the decompiler does not
    /// have is left out, and <paramref name="leaveOut"/> is told.</summary>
    public static DecompilerOptions Parse(DecompilerOptionsJson json, Action leaveOut)
    {
        var options = DecompilerOptions.Default with { LanguageVersion = json.LanguageVersion ?? DecompilerOptions.Default.LanguageVersion };
        if (json.Toggles == null)
            return options;
        foreach (var (name, value) in json.Toggles)
        {
            var toggle = DecompilerToggle.Find(name);
            if (toggle == null)
            {
                leaveOut();
                continue;
            }
            options = options.With(toggle, value);
        }
        return options;
    }

    public static WindowBoundsJson Write(WindowBounds bounds)
    {
        return new WindowBoundsJson(bounds.X, bounds.Y, bounds.Width, bounds.Height, bounds.Maximized);
    }

    public static WindowBounds Parse(WindowBoundsJson json)
    {
        return new WindowBounds(json.X, json.Y, json.Width, json.Height, json.Maximized);
    }

    public static SavedFilterStateJson Write(SavedFilterState filter)
    {
        var groups = filter.Groups?.ToDictionary(static g => g.Key, static uint[]? (g) => g.Value);
        return new SavedFilterStateJson(groups, filter.Flags, filter.Mode);
    }

    public static SavedFilterState Parse(SavedFilterStateJson json)
    {
        return new SavedFilterState(ParseGroups(json.Groups), json.Flags, json.Mode);
    }

    private static Dictionary<string, uint[]>? ParseGroups(Dictionary<string, uint[]?>? groups)
    {
        if (groups == null)
            return null;
        var kept = new Dictionary<string, uint[]>();
        foreach (var (group, values) in groups)
        {
            if (values != null)
                kept.Add(group, values);
        }
        return kept;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, Converters =
[
    typeof(EnumByName<ApiScope>), typeof(EnumByName<ApiVisibility>), typeof(EnumByName<BookmarkKind>),
    typeof(EnumByName<CodeAnchorKind>), typeof(EnumByName<CSharpVersion>), typeof(EnumByName<DecompilationLanguage>),
    typeof(EnumByName<Direction>), typeof(EnumByName<Locale>), typeof(EnumByName<MatchMode>),
    typeof(EnumByName<MemberSection>), typeof(EnumByName<SearchKinds>),
    typeof(EnumByName<TokenFormat>), typeof(EnumByName<TriState>),
])]
[JsonSerializable(typeof(SettingsFile))]
[JsonSerializable(typeof(BookmarkFileJson))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(List<JsonElement>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(DecompilationLanguage?))]
[JsonSerializable(typeof(ApiVisibility?))]
[JsonSerializable(typeof(SearchKinds?))]
[JsonSerializable(typeof(Locale?))]
[JsonSerializable(typeof(WindowBoundsJson))]
[JsonSerializable(typeof(DecompilerOptionsJson))]
[JsonSerializable(typeof(DisplayOptionsJson))]
[JsonSerializable(typeof(DockLayoutJson))]
[JsonSerializable(typeof(List<string?>))]
[JsonSerializable(typeof(List<float>))]
[JsonSerializable(typeof(List<CodeAnchorJson?>))]
[JsonSerializable(typeof(Dictionary<string, SavedFilterStateJson?>))]
[JsonSerializable(typeof(NuGetSearchJson))]
[JsonSerializable(typeof(AssemblyListJson))]
[JsonSerializable(typeof(DocumentJson))]
[JsonSerializable(typeof(BookmarkJson))]
internal sealed partial class AppSettingsJson : JsonSerializerContext;

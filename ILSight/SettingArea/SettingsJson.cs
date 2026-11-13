using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ZGF.Desktop;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.CodeViewArea;
using ILSight.BookmarkArea;
using ILSight.AppSettingArea;
using ILSight.AssemblyListArea;
using ILSight.NuGetArea;
using ILSight.DiagramArea;

namespace ILSight.SettingArea;

/// <summary>Turns what the app keeps into the shapes in this folder, and back. A file is read one field at a time, so
/// that a value which is not what its field holds leaves out that field alone, for its default to stand in,
/// and is named in <see cref="SettingsReader.LeftOut"/>; in a field holding entries (the lists, their assemblies,
/// analyses, picks and tabs, the bookmarks), an entry that can't be read leaves out that entry alone, named by where it
/// is (<c>AssemblyLists[0].Assemblies[1]</c>), the entries beside it kept. An entry that is null holds nothing, and is
/// skipped unsaid.</summary>
internal static class SettingsJson
{
    /// <summary>What <paramref name="field"/> holds, or the default when the file lacks it, holds
    /// null, or holds something else.</summary>
    public static T? Read<T>(SettingsReader file, string field, JsonTypeInfo<T> type)
    {
        if (!file.Fields.TryGetValue(field, out var value))
        {
            return default;
        }

        return Parse(file, value, type, field);
    }

    /// <summary>The elements of the array <paramref name="field"/> holds, for each to be read on its
    /// own; none when the file lacks it, holds null, or holds something else.</summary>
    public static List<JsonElement> ReadEach(SettingsReader file, string field)
    {
        return Read(file, field, AppSettingsJson.Default.ListJsonElement) ?? new List<JsonElement>();
    }

    /// <summary><paramref name="value"/>, a part of <paramref name="field"/>, as <paramref name="type"/>;
    /// the default, with the field left out, when it is something else.</summary>
    public static T? Parse<T>(SettingsReader file, JsonElement value, JsonTypeInfo<T> type, string field)
    {
        try
        {
            return value.Deserialize(type);
        }
        catch (JsonException)
        {
            LeaveOut(file, field);
            return default;
        }
        catch (NotSupportedException)
        {
            LeaveOut(file, field);
            return default;
        }
    }

    public static void LeaveOut(SettingsReader file, string field)
    {
        if (!file.LeftOut.Contains(field))
        {
            file.LeftOut.Add(field);
        }
    }

    /// <summary>Where entry <paramref name="index"/> of what is at <paramref name="location"/> is.</summary>
    public static string At(string location, int index)
    {
        return $"{location}[{index}]";
    }

    private static string At(string location, string key)
    {
        return $"{location}[\"{key}\"]";
    }

    public static SettingsFile Write(AppSettings settings)
    {
        return new SettingsFile
        {
            AssemblyLists = settings.AssemblyLists.ConvertAll(Write),
            ActiveAssemblyList = settings.ActiveAssemblyList,
            Theme = settings.Theme.Text,
            IconTheme = settings.IconTheme.Text,
            Language = settings.Language,
            Decompiler = Write(settings.Decompiler),
            FoldedOptionGroups = settings.FoldedOptionGroups,
            Display = settings.Display is null ? null : Write(settings.Display),
            Locale = settings.Locale,
            ApiVisibility = settings.ApiVisibility,
            CheckForUpdates = settings.CheckForUpdates,
            SearchKinds = settings.SearchKinds,
            Layout = settings.Layout is null ? null : DockJson.Write(settings.Layout),
            Bookmarks = settings.Bookmarks.ConvertAll(Write),
            BookmarkColumns = settings.BookmarkColumns,
            SearchColumns = settings.SearchColumns,
            ExportColumns = settings.ExportColumns,
            FlagsFilters = WriteFilters(settings.FlagsFilters),
            Window = WriteWindow(settings.Window),
            ExportWindow = WriteWindow(settings.ExportWindow),
            NuGet = settings.NuGet is null ? null : Write(settings.NuGet),
            NuGetWindow = WriteWindow(settings.NuGetWindow),
            ProcessWindow = WriteWindow(settings.ProcessWindow),
            ProcessColumns = settings.ProcessColumns,
            ProcessModuleColumns = settings.ProcessModuleColumns,
            CloseWindow = WriteWindow(settings.CloseWindow),
        };
    }

    private static Dictionary<string, SavedFilterStateJson?>? WriteFilters(Dictionary<string, SavedFilterState>? filters)
    {
        if (filters is null)
        {
            return null;
        }

        var written = new Dictionary<string, SavedFilterStateJson?>();
        foreach (var (key, filter) in filters)
        {
            written.Add(key, Write(filter));
        }

        return written;
    }

    private static WindowBoundsJson? WriteWindow(WindowBounds? bounds)
    {
        if (bounds is null)
        {
            return null;
        }

        return Write(bounds.Value);
    }

    private static AssemblyListJson Write(AssemblyListRecord list)
    {
        return new AssemblyListJson
        {
            Name = list.Name,
            Assemblies = WritePaths(list.Assemblies),
            Analyzed = list.Analyzed?.Select(static CodeAnchorJson? (a) => Write(a)).ToList(),
            Pdbs = WritePicks(list.Pdbs),
            Frameworks = WritePicks(list.Frameworks),
            Documents = list.Documents is null ? null : Write(list.Documents),
        };
    }

    /// <summary>The list at <paramref name="location"/>, without the paths, analyses, picks and documents of it that
    /// can't be read, each left out where it is; null, the list left out, without a name.</summary>
    public static AssemblyListRecord? Parse(AssemblyListJson json, SettingsReader file, string location)
    {
        if (json.Name is null)
        {
            LeaveOut(file, location);
            return null;
        }

        return new AssemblyListRecord
        {
            Name = json.Name,
            Assemblies = ListPaths(json.Assemblies, file, location + ".Assemblies"),
            Analyzed = ListAnchors(json.Analyzed, file, location + ".Analyzed"),
            Pdbs = ParsePicks(json.Pdbs, file, location + ".Pdbs"),
            Frameworks = ParsePicks(json.Frameworks, file, location + ".Frameworks"),
            Documents = json.Documents is null ? null : Parse(json.Documents, file, location + ".Documents"),
        };
    }

    private static Dictionary<string, string?>? WritePicks(Dictionary<AssemblyPath, string>? picks)
    {
        return picks?.ToDictionary(static p => p.Key.Text, static string? (p) => p.Value);
    }

    private static Dictionary<AssemblyPath, string>? ParsePicks(Dictionary<string, string?>? picks, SettingsReader file, string location)
    {
        if (picks is null)
        {
            return null;
        }

        var kept = new Dictionary<AssemblyPath, string>();
        foreach (var (text, pick) in picks)
        {
            var path = AssemblyPath.Parse(text);
            if (path is null || string.IsNullOrWhiteSpace(pick))
            {
                LeaveOut(file, At(location, text));
                continue;
            }

            kept.TryAdd(path, pick);
        }
        return kept;
    }

    private static DocumentsJson Write(DocumentsRecord documents)
    {
        var json = AppSettingsJson.Default.DocumentJson;
        var tabs = documents.Tabs.ConvertAll(t => JsonSerializer.SerializeToElement(Write(t), json));
        var layout = documents.Layout is null ? null : DockJson.Write(documents.Layout);
        return new DocumentsJson
        {
            Tabs = tabs,
            Active = documents.Active,
            Preview = documents.Preview,
            Layout = layout,
        };
    }

    private static DocumentsRecord? Parse(DocumentsJson json, SettingsReader file, string location)
    {
        if (json.Tabs is null)
        {
            return null;
        }

        var tabs = ListTabs(json.Tabs, file, location + ".Tabs")
            .Where(static t => !DockPanels.IsView(t.Panel))
            .DistinctBy(static t => t.Panel)
            .ToList();
        return new DocumentsRecord
        {
            Tabs = tabs,
            Active = json.Active,
            Preview = json.Preview,
            Layout = ParseLayout(json.Layout, file, location + ".Layout"),
        };
    }

    private static List<DocumentRecord> ListTabs(List<JsonElement> tabs, SettingsReader file, string location)
    {
        var read = new List<DocumentRecord>();
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = ParseTab(tabs[i], file, At(location, i));
            if (tab is not null)
            {
                read.Add(tab);
            }
        }
        return read;
    }

    private static DocumentRecord? ParseTab(JsonElement tab, SettingsReader file, string location)
    {
        var json = Parse(file, tab, AppSettingsJson.Default.DocumentJson, location);
        if (json is null)
        {
            return null;
        }

        var read = Parse(json, () => LeaveOut(file, location));
        if (read is null)
        {
            LeaveOut(file, location);
        }

        return read;
    }

    private static DockLayout? ParseLayout(DockLayoutJson? json, SettingsReader file, string field)
    {
        if (json is null)
        {
            return null;
        }

        var read = DockJson.ToLayout(json);
        if (read is DockLayoutRead.Parsed parsed)
        {
            return parsed.Layout;
        }

        LeaveOut(file, field);
        return null;
    }

    private static DocumentJson Write(DocumentRecord document)
    {
        return document switch
        {
            NodeDocumentRecord node => Write(node),
            DiagramDocumentRecord diagram => Write(diagram),
            ComparisonDocumentRecord comparison => Write(comparison),
            _ => throw new UnreachableException(),
        };
    }

    private static DocumentRecord? Parse(DocumentJson json, Action leaveOut)
    {
        return json switch
        {
            NodeDocumentJson node => Parse(node, leaveOut),
            DiagramDocumentJson diagram => Parse(diagram, leaveOut),
            ComparisonDocumentJson comparison => Parse(comparison),
            _ => throw new UnreachableException(),
        };
    }

    private static NodeDocumentJson Write(NodeDocumentRecord node)
    {
        var code = node.View?.Code;
        var grid = node.View?.Grid;
        return new NodeDocumentJson
        {
            Panel = node.Panel,
            Title = node.Title,
            FilePath = node.Location.FilePath.Text,
            Path = Write(node.Location),
            Language = node.Language,
            Position = code is null ? null : Write(code.Value),
            GridPosition = grid is null ? null : Write(grid.Value),
        };
    }

    private static DocumentRecord? Parse(NodeDocumentJson json, Action leaveOut)
    {
        if (json.Panel is null || json.FilePath is null)
        {
            return null;
        }

        var file = AssemblyPath.Parse(json.FilePath);
        if (file == null)
        {
            return null;
        }

        var location = FindLocation(file, json.Path, leaveOut);
        if (location is null)
        {
            return null;
        }

        return new NodeDocumentRecord
        {
            Panel = json.Panel,
            Title = json.Title,
            Location = location,
            Language = json.Language,
            View = ParseView(json.Position, json.GridPosition),
        };
    }

    private static ViewPosition? ParseView(CodeViewPositionJson? code, GridPositionJson? grid)
    {
        if (code is not null)
        {
            return ViewPosition.OfCode(Parse(code.Value));
        }

        if (grid is not null)
        {
            return ViewPosition.OfGrid(Parse(grid.Value));
        }

        return null;
    }

    private static DiagramDocumentJson Write(DiagramDocumentRecord diagram)
    {
        return new DiagramDocumentJson
        {
            Panel = diagram.Panel,
            Title = diagram.Title,
            Types = diagram.Types.Select(static TypeAnchorJson? (t) => Write(t)).ToList(),
            HomeAssemblies = diagram.HomeAssemblies is null ? null : WritePaths(diagram.HomeAssemblies),
            View = diagram.View is null ? null : Write(diagram.View),
        };
    }

    private static DocumentRecord? Parse(DiagramDocumentJson json, Action leaveOut)
    {
        if (json.Panel is null || json.Types is null)
        {
            return null;
        }

        var types = ListTypes(json.Types, leaveOut);
        var homes = json.HomeAssemblies is null ? null : ListPaths(json.HomeAssemblies, _ => leaveOut());
        return new DiagramDocumentRecord
        {
            Panel = json.Panel,
            Title = json.Title,
            Types = types,
            HomeAssemblies = homes,
            View = json.View is null ? null : Parse(json.View),
        };
    }

    private static ComparisonDocumentJson Write(ComparisonDocumentRecord comparison)
    {
        return new ComparisonDocumentJson
        {
            Panel = comparison.Panel,
            Title = comparison.Title,
            LeftPath = comparison.LeftPath.Text,
            RightPath = comparison.RightPath.Text,
            Scope = comparison.Scope,
            View = comparison.View is null ? null : new ComparisonViewJson(comparison.View.OnlyDifferences),
        };
    }

    private static DocumentRecord? Parse(ComparisonDocumentJson json)
    {
        if (json.Panel is null || json.LeftPath is null || json.RightPath is null)
        {
            return null;
        }

        var left = AssemblyPath.Parse(json.LeftPath);
        var right = AssemblyPath.Parse(json.RightPath);
        if (left == null || right == null)
        {
            return null;
        }

        return new ComparisonDocumentRecord
        {
            Panel = json.Panel,
            Title = json.Title,
            LeftPath = left,
            RightPath = right,
            Scope = json.Scope,
            View = json.View is null ? null : Parse(json.View),
        };
    }

    private static List<string?> WritePaths(List<AssemblyPath> paths)
    {
        return paths.ConvertAll(static string? (p) => p.Text);
    }

    /// <summary>The absolute paths among <paramref name="paths"/>, at <paramref name="location"/>; none when there are
    /// none. Each that is no absolute path is left out where it is.</summary>
    public static List<AssemblyPath> ListPaths(List<string?>? paths, SettingsReader file, string location)
    {
        return ListPaths(paths, i => LeaveOut(file, At(location, i)));
    }

    private static List<AssemblyPath> ListPaths(List<string?>? paths, Action<int> leaveOut)
    {
        var read = new List<AssemblyPath>();
        if (paths is null)
        {
            return read;
        }

        for (var i = 0; i < paths.Count; i++)
        {
            AddIfRead(read, paths[i], i, leaveOut);
        }
        return read;
    }

    private static void AddIfRead(List<AssemblyPath> read, string? text, int index, Action<int> leaveOut)
    {
        if (text is null)
        {
            return;
        }

        var path = AssemblyPath.Parse(text);
        if (path is null)
        {
            leaveOut(index);
            return;
        }

        read.Add(path);
    }

    /// <summary>The symbols in <paramref name="anchors"/>, at <paramref name="location"/>, that name a file; none when
    /// there are none. Each that names none is left out where it is.</summary>
    public static List<CodeAnchor> ListAnchors(List<CodeAnchorJson?>? anchors, SettingsReader file, string location)
    {
        var read = new List<CodeAnchor>();
        if (anchors is null)
        {
            return read;
        }

        for (var i = 0; i < anchors.Count; i++)
        {
            AddIfRead(read, anchors[i], file, At(location, i));
        }
        return read;
    }

    private static void AddIfRead(List<CodeAnchor> read, CodeAnchorJson? json, SettingsReader file, string location)
    {
        if (json is null)
        {
            return;
        }

        var anchor = Parse(json);
        if (anchor is null)
        {
            LeaveOut(file, location);
            return;
        }

        read.Add(anchor.Value);
    }

    private static List<TypeAnchor> ListTypes(List<TypeAnchorJson?> types, Action leaveOut)
    {
        var read = new List<TypeAnchor>();
        foreach (var json in types)
        {
            AddIfRead(read, json, leaveOut);
        }
        return read;
    }

    private static void AddIfRead(List<TypeAnchor> read, TypeAnchorJson? json, Action leaveOut)
    {
        if (json is null)
        {
            return;
        }

        var type = Parse(json);
        if (type is null)
        {
            leaveOut();
            return;
        }

        read.Add(type.Value);
    }

    private static NodePath? FindLocation(AssemblyPath filePath, SavedPathJson? path, Action leaveOut)
    {
        if (path is null)
        {
            return null;
        }

        var location = Parse(path, filePath);
        // Never the root for a path that cannot be read.
        if (location is null)
        {
            leaveOut();
        }

        return location;
    }

    private static SavedPathJson Write(NodePath place)
    {
        return new SavedPathJson
        {
            Version = SavedPathJson.CurrentVersion,
            Steps = place.Steps.Select(static SavedPathStepJson? (s) => new SavedPathStepJson
            {
                Step = SavedPlaces.Write(s.Segment),
                Label = s.Label,
            }).ToList(),
        };
    }

    private static NodePath? Parse(SavedPathJson json, AssemblyPath filePath)
    {
        if (json.Version != SavedPathJson.CurrentVersion || json.Steps is null)
        {
            return null;
        }

        var steps = new List<NodePathStep>(json.Steps.Count);
        foreach (var saved in json.Steps)
        {
            if (saved is null)
            {
                return null;
            }

            var step = Parse(saved);
            if (step is null)
            {
                return null;
            }

            steps.Add(step.Value);
        }
        return NodePath.From(filePath, steps);
    }

    private static NodePathStep? Parse(SavedPathStepJson json)
    {
        if (json.Step is null || json.Label is null)
        {
            return null;
        }

        var segment = SavedPlaces.ParseSegment(json.Step);
        if (segment == null)
        {
            return null;
        }

        return new NodePathStep { Segment = segment, Label = json.Label };
    }

    public static BookmarkJson Write(BookmarkRecord bookmark)
    {
        return new BookmarkJson
        {
            Name = bookmark.Name,
            Enabled = bookmark.Enabled,
            FileName = bookmark.FileName.Text,
            AssemblyFullName = bookmark.AssemblyFullName,
            ModuleName = bookmark.ModuleName,
            Token = bookmark.Token,
            Kind = bookmark.Kind,
            ILOffset = bookmark.ILOffset,
            LineNumber = bookmark.LineNumber,
            MemberName = bookmark.MemberName,
            LocationNodeName = bookmark.LocationNodeName,
            ViewState = bookmark.ViewState is null ? null : Write(bookmark.ViewState),
            Language = bookmark.Language,
            Path = bookmark.Location is null ? null : Write(bookmark.Location),
        };
    }

    /// <summary>The bookmark, with what the file lacks filled in; null without the absolute path of an
    /// assembly file. A place that cannot be read is no place, and <paramref name="leaveOutPlace"/> is told.</summary>
    public static BookmarkRecord? Parse(BookmarkJson json, Action leaveOutPlace)
    {
        if (json.FileName is null)
        {
            return null;
        }

        var path = AssemblyPath.Parse(json.FileName);
        if (path == null)
        {
            return null;
        }

        var location = FindLocation(path, json.Path, leaveOutPlace);
        return new BookmarkRecord
        {
            Name = json.Name ?? "",
            Enabled = json.Enabled,
            FileName = path,
            AssemblyFullName = json.AssemblyFullName ?? "",
            ModuleName = json.ModuleName ?? path.FileName,
            Token = json.Token,
            Kind = json.Kind,
            ILOffset = json.ILOffset,
            LineNumber = json.LineNumber,
            MemberName = json.MemberName ?? "",
            LocationNodeName = json.LocationNodeName,
            ViewState = json.ViewState is null ? null : Parse(json.ViewState),
            Location = location,
            Language = json.Language,
        };
    }

    private static BookmarkViewStateJson Write(BookmarkViewState view)
    {
        return new BookmarkViewStateJson
        {
            Version = view.Version,
            CaretOffset = view.CaretOffset,
            VerticalOffset = view.VerticalOffset,
            HorizontalOffset = view.HorizontalOffset,
            FoldingChecksum = view.FoldingChecksum,
            ExpandedFoldings = view.ExpandedFoldings?.Select(static BookmarkFoldingRangeJson? (f) => Write(f)).ToList(),
            RenderedLayoutSettings = view.RenderedLayoutSettings is null ? null : Write(view.RenderedLayoutSettings),
            SelectedTreeNodePath = view.SelectedTreeNodePath is null ? null : new List<string?>(view.SelectedTreeNodePath),
        };
    }

    private static BookmarkViewState Parse(BookmarkViewStateJson json)
    {
        var foldings = json.ExpandedFoldings?.OfType<BookmarkFoldingRangeJson>().Select(static f => Parse(f)).ToList();
        return new BookmarkViewState
        {
            Version = json.Version,
            CaretOffset = json.CaretOffset,
            VerticalOffset = json.VerticalOffset,
            HorizontalOffset = json.HorizontalOffset,
            FoldingChecksum = json.FoldingChecksum,
            ExpandedFoldings = foldings,
            RenderedLayoutSettings = json.RenderedLayoutSettings is null ? null : Parse(json.RenderedLayoutSettings),
            SelectedTreeNodePath = ParseTreePath(json.SelectedTreeNodePath),
        };
    }

    private static List<string>? ParseTreePath(List<string?>? path)
    {
        if (path is null)
        {
            return null;
        }

        var steps = path.OfType<string>().ToList();
        if (steps.Count != path.Count)
        {
            return null;
        }

        return steps;
    }

    private static BookmarkFoldingRangeJson Write(BookmarkFoldingRange range)
    {
        return new BookmarkFoldingRangeJson { Start = range.Start, End = range.End };
    }

    private static BookmarkFoldingRange Parse(BookmarkFoldingRangeJson json)
    {
        return new BookmarkFoldingRange { Start = json.Start, End = json.End };
    }

    private static BookmarkRenderedLayoutSettingsJson Write(BookmarkRenderedLayoutSettings settings)
    {
        return new BookmarkRenderedLayoutSettingsJson
        {
            FoldBraces = settings.FoldBraces,
            ExpandXmlDocumentationComments = settings.ExpandXmlDocumentationComments,
            ExpandMemberDefinitions = settings.ExpandMemberDefinitions,
            ExpandUsingDeclarations = settings.ExpandUsingDeclarations,
            ShowDebugInfo = settings.ShowDebugInfo,
            IndentationUseTabs = settings.IndentationUseTabs,
            IndentationSize = settings.IndentationSize,
            IndentationTabSize = settings.IndentationTabSize,
        };
    }

    private static BookmarkRenderedLayoutSettings Parse(BookmarkRenderedLayoutSettingsJson json)
    {
        return new BookmarkRenderedLayoutSettings
        {
            FoldBraces = json.FoldBraces,
            ExpandXmlDocumentationComments = json.ExpandXmlDocumentationComments,
            ExpandMemberDefinitions = json.ExpandMemberDefinitions,
            ExpandUsingDeclarations = json.ExpandUsingDeclarations,
            ShowDebugInfo = json.ShowDebugInfo,
            IndentationUseTabs = json.IndentationUseTabs,
            IndentationSize = json.IndentationSize,
            IndentationTabSize = json.IndentationTabSize,
        };
    }

    private static CodeAnchorJson Write(CodeAnchor anchor)
    {
        return new CodeAnchorJson
        {
            Kind = anchor.Kind,
            FilePath = anchor.FilePath.Text,
            Token = anchor.Token,
            ILOffset = anchor.ILOffset,
        };
    }

    private static CodeAnchor? Parse(CodeAnchorJson json)
    {
        if (json.FilePath is null)
        {
            return null;
        }

        var path = AssemblyPath.Parse(json.FilePath);
        if (path == null)
        {
            return null;
        }

        return new CodeAnchor { Kind = json.Kind, FilePath = path, Token = json.Token, ILOffset = json.ILOffset };
    }

    private static CodeViewPositionJson Write(CodeViewPosition position)
    {
        return new CodeViewPositionJson
        {
            TopLine = position.TopLine,
            ScrollX = position.ScrollX,
            Anchor = Write(position.Anchor),
            Caret = Write(position.Caret),
        };
    }

    private static CodeViewPosition Parse(CodeViewPositionJson json)
    {
        return new CodeViewPosition
        {
            TopLine = json.TopLine,
            ScrollX = json.ScrollX,
            Anchor = Parse(json.Anchor),
            Caret = Parse(json.Caret),
        };
    }

    private static TextPositionJson Write(TextPosition position)
    {
        return new TextPositionJson { Line = position.Line, Column = position.Column };
    }

    private static TextPosition Parse(TextPositionJson json)
    {
        return new TextPosition { Line = json.Line, Column = json.Column };
    }

    private static GridPositionJson Write(GridPosition position)
    {
        return new GridPositionJson
        {
            TopRow = position.TopRow,
            ScrollX = position.ScrollX,
            Row = position.Row,
            Column = position.Column,
        };
    }

    private static GridPosition Parse(GridPositionJson json)
    {
        return new GridPosition { TopRow = json.TopRow, ScrollX = json.ScrollX, Row = json.Row, Column = json.Column };
    }

    private static ComparisonViewRecord Parse(ComparisonViewJson json)
    {
        return new ComparisonViewRecord(json.OnlyDifferences);
    }

    private static TypeAnchorJson Write(TypeAnchor anchor)
    {
        return new TypeAnchorJson { AssemblyPath = anchor.AssemblyPath.Text, FullName = anchor.FullName };
    }

    private static TypeAnchor? Parse(TypeAnchorJson json)
    {
        if (json.AssemblyPath is null || json.FullName is null)
        {
            return null;
        }

        var path = AssemblyPath.Parse(json.AssemblyPath);
        if (path == null)
        {
            return null;
        }

        return new TypeAnchor { AssemblyPath = path, FullName = json.FullName };
    }

    private static DiagramViewJson Write(DiagramViewRecord view)
    {
        return new DiagramViewJson
        {
            X = view.X,
            Y = view.Y,
            Zoom = view.Zoom,
            Folds = view.Folds?.Select(static CardFoldJson? (f) => Write(f)).ToList(),
            ShowsHiddenMembers = view.ShowsHiddenMembers,
        };
    }

    private static DiagramViewRecord Parse(DiagramViewJson json)
    {
        var folds = json.Folds?.OfType<CardFoldJson>().Select(static f => Parse(f)).OfType<CardFold>().ToList();
        return new DiagramViewRecord
        {
            X = json.X,
            Y = json.Y,
            Zoom = json.Zoom,
            Folds = folds,
            ShowsHiddenMembers = json.ShowsHiddenMembers,
        };
    }

    private static CardFoldJson Write(CardFold fold)
    {
        return fold.Match(
            own: static own => new CardFoldJson { Card = Write(own.Card), Section = own.Section, InheritedFrom = null },
            inherited: static inherited => new CardFoldJson
            {
                Card = Write(inherited.Card),
                Section = null,
                InheritedFrom = inherited.From,
            });
    }

    internal static CardFold? Parse(CardFoldJson json)
    {
        if (json.Card is null)
        {
            return null;
        }

        var card = Parse(json.Card);
        if (card is null)
        {
            return null;
        }

        if (json.Section is not null && json.InheritedFrom is null)
        {
            return new CardFold.Own { Card = card.Value, Section = json.Section.Value };
        }

        if (json.Section is null && json.InheritedFrom is not null)
        {
            return new CardFold.Inherited { Card = card.Value, From = json.InheritedFrom };
        }

        return null;
    }

    private static NuGetSearchJson Write(SavedNuGetSearch search)
    {
        return new NuGetSearchJson { FeedUrl = search.FeedUrl, IncludePrerelease = search.IncludePrerelease };
    }

    public static SavedNuGetSearch? Parse(NuGetSearchJson json)
    {
        if (json.FeedUrl is null)
        {
            return null;
        }

        return new SavedNuGetSearch { FeedUrl = json.FeedUrl, IncludePrerelease = json.IncludePrerelease };
    }

    private static DisplayOptionsJson Write(DisplayOptions options)
    {
        return new DisplayOptionsJson
        {
            FontSize = options.FontSize?.Points,
            ShowLineNumbers = options.ShowLineNumbers,
            HighlightCurrentLine = options.HighlightCurrentLine,
            HighlightMatchingBrackets = options.HighlightMatchingBrackets,
            HighlightUsagesAtCaret = options.HighlightUsagesAtCaret,
            ShowOmnibar = options.ShowOmnibar,
            UiScale = options.UiScale,
            Font = options.Font is null ? null : Write(options.Font),
            TreeTokens = options.TreeTokens,
            ShowEmptyMetadataTables = options.ShowEmptyMetadataTables,
            NestedNamespaces = options.NestedNamespaces,
            SystemTitleBar = options.SystemTitleBar,
            PreviewTabs = options.PreviewTabs,
        };
    }

    public static DisplayOptions Parse(DisplayOptionsJson json)
    {
        var d = DisplayOptions.Default;
        var options = new DisplayOptions
        {
            FontSize = null,
            ShowLineNumbers = json.ShowLineNumbers ?? d.ShowLineNumbers,
            HighlightCurrentLine = json.HighlightCurrentLine ?? d.HighlightCurrentLine,
            HighlightMatchingBrackets = json.HighlightMatchingBrackets ?? d.HighlightMatchingBrackets,
            HighlightUsagesAtCaret = json.HighlightUsagesAtCaret ?? d.HighlightUsagesAtCaret,
            ShowOmnibar = json.ShowOmnibar ?? d.ShowOmnibar,
            UiScale = json.UiScale ?? d.UiScale,
            Font = json.Font is null ? null : Parse(json.Font),
            TreeTokens = json.TreeTokens ?? d.TreeTokens,
            ShowEmptyMetadataTables = json.ShowEmptyMetadataTables ?? d.ShowEmptyMetadataTables,
            NestedNamespaces = json.NestedNamespaces ?? d.NestedNamespaces,
            SystemTitleBar = json.SystemTitleBar ?? d.SystemTitleBar,
            PreviewTabs = json.PreviewTabs ?? d.PreviewTabs,
        };
        var scaled = CodeDisplay.SnapUiScale(options, options.UiScale);
        if (json.FontSize is null)
        {
            return scaled;
        }

        return CodeDisplay.SetFontSize(scaled, json.FontSize.Value);
    }

    private static CodeFontJson Write(CodeFont font)
    {
        return new CodeFontJson { Family = font.Family, Path = font.Path, FaceIndex = font.FaceIndex };
    }

    private static CodeFont? Parse(CodeFontJson json)
    {
        if (json.Family is null || json.Path is null)
        {
            return null;
        }

        return new CodeFont { Family = json.Family, Path = json.Path, FaceIndex = json.FaceIndex };
    }

    private static DecompilerOptionsJson Write(DecompilerOptions options)
    {
        return new DecompilerOptionsJson
        {
            LanguageVersion = options.LanguageVersion,
            Toggles = options.Changes.ToDictionary(static c => c.Key.Name, static c => c.Value),
        };
    }

    /// <summary>The options, a toggle at its default taken as not set; a toggle the decompiler does not
    /// have is left out, and <paramref name="leaveOut"/> is told.</summary>
    public static DecompilerOptions Parse(DecompilerOptionsJson json, Action leaveOut)
    {
        var options = DecompilerOptions.Default with { LanguageVersion = json.LanguageVersion ?? DecompilerOptions.Default.LanguageVersion };
        if (json.Toggles is null)
        {
            return options;
        }

        foreach (var (name, value) in json.Toggles)
        {
            var toggle = DecompilerToggles.FindToggle(name);
            if (toggle is null)
            {
                leaveOut();
                continue;
            }
            options = DecompilerToggles.SetToggle(options, toggle, value);
        }
        return options;
    }

    private static WindowBoundsJson Write(WindowBounds bounds)
    {
        return new WindowBoundsJson
        {
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = bounds.Maximized,
        };
    }

    public static WindowBounds Parse(WindowBoundsJson json)
    {
        return new WindowBounds(json.X, json.Y, json.Width, json.Height, json.Maximized);
    }

    private static SavedFilterStateJson Write(SavedFilterState filter)
    {
        var groups = filter.Groups?.ToDictionary(static g => g.Key, static uint[]? (g) => g.Value);
        return new SavedFilterStateJson { Groups = groups, Flags = filter.Flags, Mode = filter.Mode };
    }

    public static SavedFilterState Parse(SavedFilterStateJson json)
    {
        return new SavedFilterState { Groups = ParseGroups(json.Groups), Flags = json.Flags, Mode = json.Mode };
    }

    private static Dictionary<string, uint[]>? ParseGroups(Dictionary<string, uint[]?>? groups)
    {
        if (groups is null)
        {
            return null;
        }

        var kept = new Dictionary<string, uint[]>();
        foreach (var (group, values) in groups)
        {
            if (values is not null)
            {
                kept.Add(group, values);
            }
        }
        return kept;
    }

    /// <summary><paramref name="bounds"/> with its size kept within reason, or null when it has none.
    /// A position off every monitor is the window system's to correct when the window opens.</summary>
    public static WindowBounds? SanitizeWindow(WindowBounds? bounds)
    {
        if (bounds is null)
        {
            return null;
        }

        var b = bounds.Value;
        if (b.Width <= 0 || b.Height <= 0)
        {
            return null;
        }

        return b with
        {
            Width = Math.Clamp(b.Width, AppSettings.MinWindowWidth, AppSettings.MaxWindowSize),
            Height = Math.Clamp(b.Height, AppSettings.MinWindowHeight, AppSettings.MaxWindowSize),
        };
    }

    /// <summary>A dialog's <paramref name="bounds"/> with its size kept within reason, or null when it
    /// has none. A dialog keeps its own smallest size, so only the largest is enforced here.</summary>
    public static WindowBounds? SanitizeDialogWindow(WindowBounds? bounds)
    {
        if (bounds is null)
        {
            return null;
        }

        var b = bounds.Value;
        if (b.Width <= 0 || b.Height <= 0)
        {
            return null;
        }

        return b with
        {
            Width = Math.Min(b.Width, AppSettings.MaxWindowSize),
            Height = Math.Min(b.Height, AppSettings.MaxWindowSize),
            Maximized = false,
        };
    }

    /// <summary>What the settings file's <paramref name="text"/> holds, each value read on its own, so a value it
    /// holds wrong leaves out only that one, for its default to stand in. When values were left out, a saved layout
    /// that couldn't be restored is told among them.</summary>
    public static SettingsTextContent ParseSettingsText(string text)
    {
        Dictionary<string, JsonElement>? fields;
        try
        {
            fields = JsonSerializer.Deserialize(text, AppSettingsJson.Default.DictionaryStringJsonElement);
        }
        catch (JsonException e)
        {
            return new SettingsTextContent.NotJson(e.Message);
        }
        catch (NotSupportedException e)
        {
            return new SettingsTextContent.NotJson(e.Message);
        }
        if (fields is null)
        {
            return new SettingsTextContent.Empty();
        }

        var file = new SettingsReader { Fields = fields };
        var settings = ParseSettings(file, out var layoutProblem);
        if (file.LeftOut.Count > 0 && layoutProblem is not SettingsProblem.None)
        {
            SettingsJson.LeaveOut(file, nameof(SettingsFile.Layout));
        }

        return new SettingsTextContent.Values { Settings = settings, LeftOut = file.LeftOut, LayoutProblem = layoutProblem };
    }

    private static AppSettings ParseSettings(SettingsReader file, out SettingsProblem layoutProblem)
    {
        var json = AppSettingsJson.Default;
        var lists = ReadAssemblyLists(file);
        var active = SettingsJson.Read(file, nameof(SettingsFile.ActiveAssemblyList), json.String);
        MoveUnnamedListIntoDefault(lists, SettingsJson.Read(file, SettingsFile.OldAssemblies, json.ListString), file);
        MoveAnalysesIntoActiveList(lists, active, SettingsJson.Read(file, SettingsFile.OldAnalyzed, json.ListCodeAnchorJson), file);
        return new AppSettings
        {
            AssemblyLists = lists,
            ActiveAssemblyList = active,
            Theme = GetThemeId(SettingsJson.Read(file, nameof(SettingsFile.Theme), json.String)),
            IconTheme = GetIconThemeId(SettingsJson.Read(file, nameof(SettingsFile.IconTheme), json.String)),
            Language = SettingsJson.Read(file, nameof(SettingsFile.Language), json.NullableDecompilationLanguage) ?? DecompilationLanguage.CSharp,
            Decompiler = ReadDecompiler(file),
            FoldedOptionGroups = ListSetNames(SettingsJson.Read(file, nameof(SettingsFile.FoldedOptionGroups), json.ListString)),
            Display = ReadDisplay(file),
            Locale = SettingsJson.Read(file, nameof(SettingsFile.Locale), json.NullableLocale),
            ApiVisibility = GetApiVisibility(
                SettingsJson.Read(file, nameof(SettingsFile.ApiVisibility), json.NullableApiVisibility),
                SettingsJson.Read(file, SettingsFile.OldShowNonPublic, json.NullableBoolean)),
            CheckForUpdates = SettingsJson.Read(file, nameof(SettingsFile.CheckForUpdates), json.NullableBoolean) ?? true,
            SearchKinds = SettingsJson.Read(file, nameof(SettingsFile.SearchKinds), json.NullableSearchKinds) ?? SearchKinds.TypesAndMembers,
            Layout = ReadLayout(file, out layoutProblem),
            Bookmarks = ReadBookmarks(file),
            BookmarkColumns = SettingsJson.Read(file, nameof(SettingsFile.BookmarkColumns), json.ListSingle),
            SearchColumns = SettingsJson.Read(file, nameof(SettingsFile.SearchColumns), json.ListSingle),
            ExportColumns = SettingsJson.Read(file, nameof(SettingsFile.ExportColumns), json.ListSingle),
            FlagsFilters = ReadFlagsFilters(file),
            Window = SanitizeWindow(ReadWindow(file, nameof(SettingsFile.Window))),
            ExportWindow = SanitizeDialogWindow(ReadWindow(file, nameof(SettingsFile.ExportWindow))),
            NuGet = ReadNuGet(file),
            NuGetWindow = SanitizeDialogWindow(ReadWindow(file, nameof(SettingsFile.NuGetWindow))),
            ProcessWindow = SanitizeDialogWindow(ReadWindow(file, nameof(SettingsFile.ProcessWindow))),
            ProcessColumns = SettingsJson.Read(file, nameof(SettingsFile.ProcessColumns), json.ListSingle),
            ProcessModuleColumns = SettingsJson.Read(file, nameof(SettingsFile.ProcessModuleColumns), json.ListSingle),
            CloseWindow = SanitizeDialogWindow(ReadWindow(file, nameof(SettingsFile.CloseWindow))),
        };
    }

    private static DecompilerOptions ReadDecompiler(SettingsReader file)
    {
        const string field = nameof(SettingsFile.Decompiler);
        var saved = SettingsJson.Read(file, field, AppSettingsJson.Default.DecompilerOptionsJson);
        if (saved is null)
        {
            return DecompilerOptions.Default;
        }

        return SettingsJson.Parse(saved, () => SettingsJson.LeaveOut(file, field));
    }

    private static DisplayOptions? ReadDisplay(SettingsReader file)
    {
        var saved = SettingsJson.Read(file, nameof(SettingsFile.Display), AppSettingsJson.Default.DisplayOptionsJson);
        if (saved is null)
        {
            return null;
        }

        return SettingsJson.Parse(saved);
    }

    private static WindowBounds? ReadWindow(SettingsReader file, string field)
    {
        var saved = SettingsJson.Read(file, field, AppSettingsJson.Default.WindowBoundsJson);
        if (saved is null)
        {
            return null;
        }

        return SettingsJson.Parse(saved);
    }

    private static SavedNuGetSearch? ReadNuGet(SettingsReader file)
    {
        var saved = SettingsJson.Read(file, nameof(SettingsFile.NuGet), AppSettingsJson.Default.NuGetSearchJson);
        if (saved is null)
        {
            return null;
        }

        return SettingsJson.Parse(saved);
    }

    private static List<AssemblyListRecord> ReadAssemblyLists(SettingsReader file)
    {
        const string field = nameof(SettingsFile.AssemblyLists);
        var saved = SettingsJson.ReadEach(file, field);
        var lists = new List<AssemblyListRecord>();
        for (var i = 0; i < saved.Count; i++)
        {
            var list = ParseAssemblyList(file, saved[i], SettingsJson.At(field, i));
            if (list is not null)
            {
                lists.Add(list);
            }
        }
        return lists;
    }

    private static AssemblyListRecord? ParseAssemblyList(SettingsReader file, JsonElement list, string location)
    {
        if (list.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var saved = SettingsJson.Parse(file, list, AppSettingsJson.Default.AssemblyListJson, location);
        if (saved is null)
        {
            return null;
        }

        return SettingsJson.Parse(saved, file, location);
    }

    private static List<BookmarkRecord> ReadBookmarks(SettingsReader file)
    {
        const string field = nameof(SettingsFile.Bookmarks);
        var saved = SettingsJson.ReadEach(file, field);
        var bookmarks = new List<BookmarkRecord>();
        for (var i = 0; i < saved.Count; i++)
        {
            var bookmark = ParseBookmark(file, saved[i], SettingsJson.At(field, i));
            if (bookmark is not null)
            {
                bookmarks.Add(bookmark);
            }
        }
        return bookmarks;
    }

    private static BookmarkRecord? ParseBookmark(SettingsReader file, JsonElement bookmark, string location)
    {
        if (bookmark.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var saved = SettingsJson.Parse(file, bookmark, AppSettingsJson.Default.BookmarkJson, location);
        if (saved is null)
        {
            return null;
        }

        var read = SettingsJson.Parse(saved, () => SettingsJson.LeaveOut(file, location));
        if (read is null)
        {
            SettingsJson.LeaveOut(file, location);
        }

        return read;
    }

    private static List<string>? ListSetNames(List<string?>? names)
    {
        return names?.OfType<string>().ToList();
    }

    // Null when the dock space cannot be given the layout, for the panels to be where they start out.
    private static DockLayout? ReadLayout(SettingsReader file, out SettingsProblem problem)
    {
        problem = new SettingsProblem.None();
        var saved = SettingsJson.Read(file, nameof(SettingsFile.Layout), AppSettingsJson.Default.DockLayoutJson);
        if (saved is null)
        {
            return null;
        }

        (var layout, problem) = DockJson.ToLayout(saved).Match<(DockLayout?, SettingsProblem)>(
            parsed: static parsed => (parsed.Layout, new SettingsProblem.None()),
            partsMissing: static _ => (null, new SettingsProblem.LayoutPartsMissing()),
            idsRepeated: static _ => (null, new SettingsProblem.LayoutPartsRepeated()));
        return layout;
    }

    private static Dictionary<string, SavedFilterState>? ReadFlagsFilters(SettingsReader file)
    {
        var saved = SettingsJson.Read(file, nameof(SettingsFile.FlagsFilters), AppSettingsJson.Default.DictionaryStringSavedFilterStateJson);
        if (saved is null)
        {
            return null;
        }

        var filters = new Dictionary<string, SavedFilterState>();
        foreach (var (key, filter) in saved)
        {
            if (filter is not null)
            {
                filters.Add(key, SettingsJson.Parse(filter));
            }
        }
        return filters;
    }

    // Files from before lists had names kept one unnamed list.
    private static void MoveUnnamedListIntoDefault(List<AssemblyListRecord> lists, List<string?>? unnamed, SettingsReader file)
    {
        if (unnamed is null)
        {
            return;
        }

        if (lists.Count > 0)
        {
            return;
        }

        lists.Add(new AssemblyListRecord
        {
            Name = SavedAssemblyLists.DefaultName,
            Assemblies = SettingsJson.ListPaths(unnamed, file, SettingsFile.OldAssemblies),
        });
    }

    // Files from before each list kept its own analyses kept them apart from the lists.
    private static void MoveAnalysesIntoActiveList(List<AssemblyListRecord> lists, string? active, List<CodeAnchorJson?>? analyzed, SettingsReader file)
    {
        if (analyzed is null)
        {
            return;
        }

        if (lists.Count == 0)
        {
            lists.Add(new AssemblyListRecord
            {
                Name = SavedAssemblyLists.DefaultName,
                Assemblies = new List<AssemblyPath>(),
            });
        }

        var index = GetActiveListIndex(lists, active);
        var list = lists[index];
        var hasAnalyses = list.Analyzed is not null && list.Analyzed.Count > 0;
        if (hasAnalyses)
        {
            return;
        }

        lists[index] = list with { Analyzed = SettingsJson.ListAnchors(analyzed, file, SettingsFile.OldAnalyzed) };
    }

    private static int GetActiveListIndex(List<AssemblyListRecord> lists, string? active)
    {
        if (active is null)
        {
            return 0;
        }

        var index = lists.FindIndex(l => string.Equals(l.Name.Trim(), active.Trim(), StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return 0;
        }

        return index;
    }

    private static ApiVisibility GetApiVisibility(ApiVisibility? saved, bool? showNonPublic)
    {
        // Files from before the visibility had three choices kept a non-public switch.
        if (showNonPublic == false)
        {
            return ApiVisibility.PublicOnly;
        }

        return saved ?? ApiVisibility.AllButCompilerGenerated;
    }

    private static ThemeId<ThemeFile> GetThemeId(string? saved)
    {
        if (saved is null)
        {
            return Theming.DarkId;
        }

        // Files from before themes were files kept the theme's name, not its id.
        var old = FindOldThemeId(saved);
        if (old is not null)
        {
            return ThemeId<ThemeFile>.Of(old);
        }

        return ThemeId<ThemeFile>.Parse(saved) ?? Theming.DarkId;
    }

    private static string? FindOldThemeId(string name)
    {
        return name switch
        {
            "Dark" => "dark",
            "Light" => "light",
            "GitHubDark" => "github-dark",
            "OneDarkPro" => "one-dark-pro",
            "Dracula" => "dracula",
            "Monokai" => "monokai",
            "CatppuccinMocha" => "catppuccin-mocha",
            "GitHubLight" => "github-light",
            "SolarizedLight" => "solarized-light",
            _ => null,
        };
    }

    private static ThemeId<IconThemeFile> GetIconThemeId(string? saved)
    {
        if (saved is null)
        {
            return IconTheming.ReferenceId;
        }

        return ThemeId<IconThemeFile>.Parse(saved) ?? IconTheming.ReferenceId;
    }

    /// <summary>The text of the settings file that keeps <paramref name="settings"/>.</summary>
    public static string ToJson(AppSettings settings)
    {
        return JsonSerializer.Serialize(Write(settings), AppSettingsJson.Default.SettingsFile);
    }
}

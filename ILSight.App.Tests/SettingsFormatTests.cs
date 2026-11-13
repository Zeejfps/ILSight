using System.Text.Json;
using System.Text.Json.Nodes;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Comparison;
using ILSight.Core.Diagrams;
using ILSight.Core.Metadata;
using static ILSight.App.Tests.TestPaths;
using ILSight.CodeViewArea;
using ILSight.BookmarkArea;
using ILSight.AppSettingArea;
using ILSight.AssemblyListArea;
using ILSight.DiagramArea;
using ILSight.SettingArea;

namespace ILSight.App.Tests;

public sealed class SettingsFormatTests : IDisposable
{
    private const string SettingsFixture = "settings.json";
    private const string BookmarksFixture = "ILSpy.Bookmarks.json";
    private const string FixtureFolderInFile = @"C:\\Fixture\\";

    private static readonly AssemblyPath A = PathOf(Rooted("Fixture/A.dll"));
    private static readonly AssemblyPath B = PathOf(Rooted("Fixture/B.dll"));
    private static readonly TypeAnchor Calculator = new TypeAnchor { AssemblyPath = A, FullName = "Ns.Calculator" };
    private static readonly TypeAnchorJson CalculatorJson = new TypeAnchorJson
    {
        AssemblyPath = A.Text,
        FullName = "Ns.Calculator",
    };

    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public void Settings_with_every_field_set_are_written_as_the_fixture()
    {
        var path = _temp.PathOf(SettingsFixture);
        var settings = ExpectedSettings() with { FilePath = FullFilePath.Of(path) };

        Assert.IsType<SettingsWriteResult.Written>(TempSettings.Save(settings));

        AssertSameJson(Fixture(SettingsFixture), File.ReadAllText(path));
    }

    [Fact]
    public void A_saved_fold_of_a_section_is_a_cards_own()
    {
        var fold = SettingsJson.Parse(new CardFoldJson
        {
            Card = CalculatorJson,
            Section = MemberSection.Methods,
            InheritedFrom = null,
        });

        Assert.Equal(new CardFold.Own { Card = Calculator, Section = MemberSection.Methods }, fold);
    }

    [Fact]
    public void A_saved_fold_of_a_base_type_is_what_a_card_inherits()
    {
        var fold = SettingsJson.Parse(new CardFoldJson
        {
            Card = CalculatorJson,
            Section = null,
            InheritedFrom = "Base",
        });

        Assert.Equal(new CardFold.Inherited { Card = Calculator, From = "Base" }, fold);
    }

    [Fact]
    public void A_saved_fold_naming_both_a_section_and_a_base_type_is_none()
    {
        Assert.Null(SettingsJson.Parse(new CardFoldJson
        {
            Card = CalculatorJson,
            Section = MemberSection.Methods,
            InheritedFrom = "Base",
        }));
    }

    [Fact]
    public void A_saved_fold_naming_neither_a_section_nor_a_base_type_is_none()
    {
        Assert.Null(SettingsJson.Parse(new CardFoldJson
        {
            Card = CalculatorJson,
            Section = null,
            InheritedFrom = null,
        }));
    }

    [Fact]
    public void The_settings_fixture_loads_whole_and_is_saved_as_it_was()
    {
        var path = _temp.Write(SettingsFixture, Fixture(SettingsFixture));

        var read = AppRoot.ReadSettingsFile(FullFilePath.Of(path));
        var loaded = read.Settings;
        var problem = read.Problem;
        Assert.IsType<SettingsProblem.None>(problem);
        Assert.IsType<SettingsWriteResult.Written>(TempSettings.Save(loaded));

        AssertSameJson(Fixture(SettingsFixture), File.ReadAllText(path));
    }

    [Fact]
    public void Bookmarks_with_every_field_set_are_written_as_the_fixture()
    {
        var path = _temp.PathOf(BookmarksFixture);
        Directory.CreateDirectory(_temp.Path);

        Assert.IsType<BookmarksWriteResult.Written>(BookmarkFile.Write(path, ExpectedBookmarks()));

        AssertSameJson(Fixture(BookmarksFixture), File.ReadAllText(path));
    }

    [Fact]
    public void The_bookmarks_fixture_reads_whole_and_is_written_as_it_was()
    {
        var path = _temp.Write(BookmarksFixture, Fixture(BookmarksFixture));

        var read = Assert.IsType<BookmarksRead.Read>(BookmarkFile.Read(path));
        Assert.Empty(read.Problems);
        Assert.IsType<BookmarksWriteResult.Written>(BookmarkFile.Write(path, read.Bookmarks));

        AssertSameJson(Fixture(BookmarksFixture), File.ReadAllText(path));
    }

    /// <summary>The fixture file <paramref name="name"/>, its paths as this OS writes them: the files hold
    /// them as Windows does, and on any other OS those are no absolute paths.</summary>
    private static string Fixture(string name)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Settings", name))
            .Replace(FixtureFolderInFile, JsonEncodedText.Encode(Rooted("Fixture/")).Value);
    }

    private static void AssertSameJson(string expected, string actual)
    {
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), actual);
    }

    private static AppSettings ExpectedSettings()
    {
        var braces = DecompilerToggles.FindToggle("AlwaysUseBraces");
        Assert.NotNull(braces);
        return new AppSettings()
        {
            AssemblyLists =
            [
                new AssemblyListRecord
                {
                    Name = "Work",
                    Assemblies = [A, B],
                    Analyzed = [new CodeAnchor
                    {
                        Kind = CodeAnchorKind.Statement,
                        FilePath = A,
                        Token = 0x06000001,
                        ILOffset = 7,
                    }],
                    Pdbs = new Dictionary<AssemblyPath, string> { [A] = Rooted("Fixture/A.pdb") },
                    Frameworks = new Dictionary<AssemblyPath, string> { [B] = ".NETCoreApp,Version=v8.0" },
                    Documents = ExpectedDocuments(),
                },
                new AssemblyListRecord { Name = "Play", Assemblies = [B], Analyzed = [] },
            ],
            ActiveAssemblyList = "Work",
            Theme = ThemeId<ThemeFile>.Of("monokai"),
            IconTheme = ThemeId<IconThemeFile>.Of("custom-icons"),
            Language = DecompilationLanguage.IL,
            Decompiler = DecompilerToggles.SetToggle(new DecompilerOptions { LanguageVersion = CSharpVersion.CSharp12 }, braces, false),
            FoldedOptionGroups = ["Other"],
            Display = new DisplayOptions
            {
                FontSize = CodeFontSize.Clamp(14f),
                ShowLineNumbers = false,
                HighlightCurrentLine = true,
                HighlightMatchingBrackets = false,
                HighlightUsagesAtCaret = true,
                ShowOmnibar = false,
                UiScale = 1.25f,
                Font = new CodeFont
                {
                    Family = "Mono",
                    Path = @"C:\Fonts\Mono.ttf",
                    FaceIndex = 1,
                },
                TreeTokens = TokenFormat.Hex,
                ShowEmptyMetadataTables = true,
                NestedNamespaces = true,
                SystemTitleBar = true,
                PreviewTabs = false,
            },
            Locale = Locale.De,
            ApiVisibility = ApiVisibility.All,
            SearchKinds = SearchKinds.Types | SearchKinds.Literals,
            Layout = new DockLayout
            {
                Root = new DockSplit
                {
                    Id = 1, Direction = Direction.Horizontal, Ratio = 0.25f,
                    First = new DockGroup { Id = 2, Panels = ["tree"], Active = "tree" },
                    Second = new DockGroup { Id = 3, Panels = ["documents", "search"], Active = "search" },
                },
                Windows = [new DockWindow { Id = 4, Root = new DockGroup { Id = 5, Panels = ["analyzer"], Active = "analyzer" }, X = 10f, Y = 20f, Width = 300f, Height = 200f }],
                NextId = 6,
            },
            Bookmarks = ExpectedBookmarks(),
            BookmarkColumns = [1.5f, 2.5f],
            SearchColumns = [3.5f, 1f],
            ExportColumns = [2f, 0.5f],
            FlagsFilters = new Dictionary<string, SavedFilterState>
            {
                ["TypeDef/Attributes"] = new SavedFilterState
                {
                    Groups = new Dictionary<string, uint[]> { ["Visibility"] = [1u, 2u] },
                    Flags = new Dictionary<string, TriState> { ["Sealed"] = TriState.Required },
                    Mode = MatchMode.Any,
                },
            },
            Window = new WindowBounds(10, 20, 1200, 800, true),
            ExportWindow = new WindowBounds(30, 40, 900, 600, false),
            NuGet = new SavedNuGetSearch { FeedUrl = "https://api.nuget.org/v3/index.json", IncludePrerelease = true },
            NuGetWindow = new WindowBounds(50, 60, 700, 500, false),
            ProcessWindow = new WindowBounds(70, 80, 900, 600, false),
            ProcessColumns = [2f, 1f, 3f, 1f],
            ProcessModuleColumns = [1f, 4f],
            CloseWindow = new WindowBounds(90, 100, 560, 600, false),
        };
    }

    private static DocumentsRecord ExpectedDocuments()
    {
        var code = ViewPosition.OfCode(new CodeViewPosition
        {
            TopLine = 12,
            ScrollX = 3.5f,
            Anchor = new TextPosition
            {
                Line = 13,
                Column = 2,
            },
            Caret = new TextPosition
            {
                Line = 14,
                Column = 5,
            },
        });
        var grid = ViewPosition.OfGrid(new GridPosition { TopRow = 4, ScrollX = 1.5f, Row = 6, Column = 2 });
        var view = new DiagramViewRecord
        {
            X = 10.5f,
            Y = -20.25f,
            Zoom = 1.5f,
            Folds =
            [
                new CardFold.Own { Card = Calculator, Section = MemberSection.Methods },
                new CardFold.Inherited { Card = Calculator, From = "Base" },
            ],
            ShowsHiddenMembers = true,
        };
        DocumentRecord[] tabs =
        [
            new NodeDocumentRecord
            {
                Panel = "document-1",
                Title = "Calculator",
                Location = CalculatorPlace(),
                Language = DecompilationLanguage.IL,
                View = code,
            },
            new NodeDocumentRecord
            {
                Panel = "document-2",
                Title = "Other",
                Location = OtherPlace(),
                Language = DecompilationLanguage.ILWithCSharp,
                View = grid,
            },
            new DiagramDocumentRecord
            {
                Panel = "document-3",
                Title = "Diagram",
                Types = [Calculator, new TypeAnchor { AssemblyPath = B, FullName = "Ns.Other" }],
                HomeAssemblies = [A],
                View = view,
            },
            new ComparisonDocumentRecord
            {
                Panel = "document-4",
                Title = "Comparison",
                LeftPath = A,
                RightPath = B,
                Scope = ApiScope.AllDeclared,
                View = new ComparisonViewRecord(true),
            },
        ];
        var layout = new DockLayout
        {
            Root = new DockSplit
            {
                Id = 1, Direction = Direction.Horizontal, Ratio = 0.75f,
                First = new DockGroup { Id = 2, Panels = ["document-1", "document-2"], Active = "document-2" },
                Second = new DockGroup { Id = 3, Panels = ["document-3"], Active = "document-3" },
            },
            Windows = [new DockWindow { Id = 4, Root = new DockGroup { Id = 5, Panels = ["document-4"], Active = "document-4" }, X = 5f, Y = 6f, Width = 400f, Height = 300f }],
            NextId = 6,
        };
        return new DocumentsRecord
        {
            Tabs = tabs.ToList(),
            Active = "document-3",
            Preview = "document-4",
            Layout = layout,
        };
    }

    private static List<BookmarkRecord> ExpectedBookmarks()
    {
        var view = new BookmarkViewState
        {
            Version = 2,
            CaretOffset = 100,
            VerticalOffset = 50.5,
            HorizontalOffset = 7.25,
            FoldingChecksum = 1234,
            ExpandedFoldings = [new BookmarkFoldingRange { Start = 1, End = 9 }],
            RenderedLayoutSettings = new BookmarkRenderedLayoutSettings
            {
                FoldBraces = true,
                ExpandXmlDocumentationComments = true,
                ExpandMemberDefinitions = true,
                ExpandUsingDeclarations = true,
                ShowDebugInfo = true,
                IndentationUseTabs = true,
                IndentationSize = 8,
                IndentationTabSize = 6,
            },
            SelectedTreeNodePath = ["A", "Ns", "Calculator"],
        };
        return
        [
            new BookmarkRecord
            {
                Name = "Mark",
                Enabled = true,
                FileName = A,
                AssemblyFullName = "A, Version=1.0.0.0",
                ModuleName = "A.dll",
                Token = 0x06000002,
                Kind = BookmarkKind.Body,
                ILOffset = 12,
                LineNumber = 34,
                MemberName = "Ns.Calculator.Add",
                LocationNodeName = "Add(int, int) : int",
                ViewState = view,
                Location = CalculatorPlace(),
                Language = DecompilationLanguage.IL,
            },
            new BookmarkRecord
            {
                Name = "Old",
                Enabled = true,
                FileName = B,
                AssemblyFullName = "B, Version=2.0.0.0",
                ModuleName = "B.dll",
                Token = 0x02000003,
                Kind = BookmarkKind.Line,
                ILOffset = 5,
                LineNumber = 56,
                MemberName = "Ns.Other",
                LocationNodeName = "Other",
                ViewState = null,
            },
        ];
    }

    private static NodePath CalculatorPlace()
    {
        return NodePath.From(A,
        [
            new NodePathStep { Segment = new NodePathSegment.Namespace("Ns"), Label = "Ns" },
            new NodePathStep { Segment = new NodePathSegment.Definition("T:Ns.Calculator"), Label = "Calculator" },
        ]);
    }

    private static NodePath OtherPlace()
    {
        return NodePath.From(B,
        [
            new NodePathStep { Segment = new NodePathSegment.Namespace("Ns"), Label = "Ns" },
            new NodePathStep { Segment = new NodePathSegment.Definition("T:Ns.Other"), Label = "Other" },
        ]);
    }
}

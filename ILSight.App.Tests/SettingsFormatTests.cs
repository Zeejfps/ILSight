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
    private static readonly TypeAnchor Calculator = new(A, "Ns.Calculator");
    private static readonly TypeAnchorJson CalculatorJson = new(A.Text, "Ns.Calculator");

    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public void Settings_with_every_field_set_are_written_as_the_fixture()
    {
        var path = _temp.PathOf(SettingsFixture);
        var settings = ExpectedSettings();
        settings.FilePath = FullFilePath.Of(path);

        Assert.IsType<SettingsWriteResult.Written>(TempSettings.Save(settings));

        AssertSameJson(Fixture(SettingsFixture), File.ReadAllText(path));
    }

    [Fact]
    public void A_saved_fold_of_a_section_is_a_cards_own()
    {
        var fold = SettingsJson.Parse(new CardFoldJson(CalculatorJson, MemberSection.Methods, null));

        Assert.Equal(new CardFold.Own(Calculator, MemberSection.Methods), fold);
    }

    [Fact]
    public void A_saved_fold_of_a_base_type_is_what_a_card_inherits()
    {
        var fold = SettingsJson.Parse(new CardFoldJson(CalculatorJson, null, "Base"));

        Assert.Equal(new CardFold.Inherited(Calculator, "Base"), fold);
    }

    [Fact]
    public void A_saved_fold_naming_both_a_section_and_a_base_type_is_none()
    {
        Assert.Null(SettingsJson.Parse(new CardFoldJson(CalculatorJson, MemberSection.Methods, "Base")));
    }

    [Fact]
    public void A_saved_fold_naming_neither_a_section_nor_a_base_type_is_none()
    {
        Assert.Null(SettingsJson.Parse(new CardFoldJson(CalculatorJson, null, null)));
    }

    [Fact]
    public void The_settings_fixture_loads_whole_and_is_saved_as_it_was()
    {
        var path = _temp.Write(SettingsFixture, Fixture(SettingsFixture));

        var (loaded, problem) = AppRoot.ReadSettingsFile(FullFilePath.Of(path));
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
                new AssemblyListRecord("Work", [A, B],
                    [new CodeAnchor(CodeAnchorKind.Statement, A, 0x06000001, 7)],
                    new Dictionary<AssemblyPath, string> { [A] = Rooted("Fixture/A.pdb") },
                    new Dictionary<AssemblyPath, string> { [B] = ".NETCoreApp,Version=v8.0" },
                    ExpectedDocuments()),
                new AssemblyListRecord("Play", [B], []),
            ],
            ActiveAssemblyList = "Work",
            Theme = ThemeId<ThemeFile>.Of("monokai"),
            IconTheme = ThemeId<IconThemeFile>.Of("custom-icons"),
            Language = DecompilationLanguage.IL,
            Decompiler = DecompilerToggles.SetToggle(new DecompilerOptions { LanguageVersion = CSharpVersion.CSharp12 }, braces, false),
            FoldedOptionGroups = ["Other"],
            Display = new DisplayOptions(CodeFontSize.Clamp(14f), false, true, false, true, false, 1.25f, new CodeFont("Mono", @"C:\Fonts\Mono.ttf", 1),
                TokenFormat.Hex, true, true, true, false),
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
            NuGet = new SavedNuGetSearch("https://api.nuget.org/v3/index.json", true),
            NuGetWindow = new WindowBounds(50, 60, 700, 500, false),
            ProcessWindow = new WindowBounds(70, 80, 900, 600, false),
            ProcessColumns = [2f, 1f, 3f, 1f],
            ProcessModuleColumns = [1f, 4f],
            CloseWindow = new WindowBounds(90, 100, 560, 600, false),
        };
    }

    private static DocumentsRecord ExpectedDocuments()
    {
        var code = ViewPosition.OfCode(new CodeViewPosition(12, 3.5f, new TextPosition(13, 2), new TextPosition(14, 5)));
        var grid = ViewPosition.OfGrid(new GridPosition(4, 1.5f, 6, 2));
        var view = new DiagramViewRecord(10.5f, -20.25f, 1.5f, [new CardFold.Own(Calculator, MemberSection.Methods), new CardFold.Inherited(Calculator, "Base")], true);
        DocumentRecord[] tabs =
        [
            new NodeDocumentRecord("document-1", "Calculator", CalculatorPlace(), DecompilationLanguage.IL, code),
            new NodeDocumentRecord("document-2", "Other", OtherPlace(), DecompilationLanguage.ILWithCSharp, grid),
            new DiagramDocumentRecord("document-3", "Diagram", [Calculator, new TypeAnchor(B, "Ns.Other")], [A], view),
            new ComparisonDocumentRecord("document-4", "Comparison", A, B, ApiScope.AllDeclared, new ComparisonViewRecord(true)),
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
        return new DocumentsRecord(tabs.ToList(), "document-3", "document-4", layout);
    }

    private static List<BookmarkRecord> ExpectedBookmarks()
    {
        var view = new BookmarkViewState(2, 100, 50.5, 7.25, 1234, [new BookmarkFoldingRange(1, 9)],
            new BookmarkRenderedLayoutSettings(true, true, true, true, true, true, 8, 6), ["A", "Ns", "Calculator"]);
        return
        [
            new BookmarkRecord("Mark", true, A, "A, Version=1.0.0.0", "A.dll", 0x06000002, BookmarkKind.Body, 12, 34,
                "Ns.Calculator.Add", "Add(int, int) : int", view, CalculatorPlace(), DecompilationLanguage.IL),
            new BookmarkRecord("Old", true, B, "B, Version=2.0.0.0", "B.dll", 0x02000003, BookmarkKind.Line, 5, 56,
                "Ns.Other", "Other", null),
        ];
    }

    private static NodePath CalculatorPlace()
    {
        return NodePath.From(A,
        [
            new NodePathStep(new NodePathSegment.Namespace("Ns"), "Ns"),
            new NodePathStep(new NodePathSegment.Definition("T:Ns.Calculator"), "Calculator"),
        ]);
    }

    private static NodePath OtherPlace()
    {
        return NodePath.From(B,
        [
            new NodePathStep(new NodePathSegment.Namespace("Ns"), "Ns"),
            new NodePathStep(new NodePathSegment.Definition("T:Ns.Other"), "Other"),
        ]);
    }
}

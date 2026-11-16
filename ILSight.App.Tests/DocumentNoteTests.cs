using ILSight.BookmarkArea;
using ILSight.CommandArea;
using ILSight.Core;
using ILSight.Core.ResourceArea;

namespace ILSight.App.Tests;

public sealed class DocumentNoteTests
{
    private static readonly string PathsSamplePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Paths", "Before", "PathsSample.dll");

    [Fact]
    public async Task A_reference_read_by_name_only_is_noted_in_the_apps_language_and_the_code_is_the_same_in_every_language()
    {
        using var temp = new TempDir();
        var sample = temp.Copy(PathsSamplePath, "PathsSample.dll");
        temp.Write("PathsLibrary.dll", "no assembly");
        var overloads = await new AssemblyList { Runtime = TestRuntime.Installed }.Open(sample).ReadRootAsync().GetChildAsync("PathsSample", "Overloads");

        var english = await NodeContentLoader.DecompileAsync(TestSession.Shared, overloads, DecompilationLanguage.CSharp, DecompilerOptions.Default,
            DocumentNoteTexts.For(Locale.En)).GetValueAsync();
        var japanese = await NodeContentLoader.DecompileAsync(TestSession.Shared, overloads, DecompilationLanguage.CSharp, DecompilerOptions.Default,
            DocumentNoteTexts.For(Locale.Ja)).GetValueAsync();

        Assert.StartsWith("// Warning: the reference PathsLibrary was read by name only: ", english.Lines[0].Text);
        Assert.EndsWith("Reload PathsSample.dll once it can be read.", english.Lines[0].Text);
        Assert.StartsWith("// 警告: 参照 PathsLibrary は名前だけで読み込まれました。", japanese.Lines[0].Text);
        Assert.EndsWith("読み込めるようになったら PathsSample.dll を再読み込みしてください。", japanese.Lines[0].Text);
        Assert.Contains("Take(", Documents.GetText(japanese));
        Assert.Equal(english.Lines.Skip(1).Select(static l => l.Text), japanese.Lines.Skip(1).Select(static l => l.Text));
    }

    [Fact]
    public async Task A_note_in_an_open_tab_follows_a_language_switch_and_the_code_is_not_decompiled_again()
    {
        using var temp = new TempDir();
        var sample = temp.Copy(PathsSamplePath, "PathsSample.dll");
        temp.Write("PathsLibrary.dll", "no assembly");
        var app = new AppDriver();
        app.Open([AssemblyPath.Of(sample)]);
        app.Settle();
        var assembly = app.AppState.ListSession.Assemblies.Assemblies.Single(a => a.FilePath.FileName == "PathsSample.dll");
        var overloads = await assembly.ReadRootAsync().GetChildAsync("PathsSample", "Overloads");
        app.Show(overloads, frames: 2);
        var tab = Assert.Single(app.AppState.ListSession.Navigation.OpenTabs);
        var made = tab.Document;
        var view = app.Ui.Documents.GetView(tab).Code;
        Assert.Same(made, view.Layout.Document);
        Assert.StartsWith("// Warning: the reference PathsLibrary was read by name only: ", made.Lines[0].Text);
        view.Selection.Select(new TextPosition { Line = 0, Column = made.Lines[0].Text.Length }, new TextPosition { Line = 2, Column = 0 });

        app.AppState.Locale = Locale.Ja;
        app.Run(2);

        Assert.Same(made, tab.Document);
        var shown = view.Layout.Document;
        Assert.NotNull(shown);
        Assert.Same(made, shown.AsMade);
        Assert.StartsWith("// 警告: 参照 PathsLibrary は名前だけで読み込まれました。", shown.Lines[0].Text);
        Assert.EndsWith("読み込めるようになったら PathsSample.dll を再読み込みしてください。", shown.Lines[0].Text);
        Assert.Equal(made.Lines.Skip(1).Select(static l => l.Text), shown.Lines.Skip(1).Select(static l => l.Text));
        Assert.Equal(Math.Min(made.Lines[0].Text.Length, shown.Lines[0].Text.Length), view.Selection.Anchor.Column);
        Assert.Equal(new TextPosition { Line = 2, Column = 0 }, view.Selection.Caret);

        app.AppState.Locale = Locale.En;
        app.Run(2);

        Assert.Same(made, view.Layout.Document);
    }

    [Fact]
    public async Task A_cut_short_BMP_is_noted_in_the_apps_language_and_its_bytes_are_shown_the_same_in_every_language()
    {
        using var temp = new TempDir();
        var whole = Picture("logo.bmp");
        var path = BrokenAssemblies.WriteWithPicture(temp, "cut.bmp", whole[..20]);
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(path);
        var picture = await assembly.ReadRootAsync().GetChildAsync("Resources", "cut.bmp");

        var englishRead = await NodeContentLoader.ReadContentAsync(TestSession.Shared, picture, DecompilationLanguage.CSharp, DecompilerOptions.Default,
            DocumentNoteTexts.For(Locale.En), firstGridRows: 1).GetValueAsync();
        var japaneseRead = await NodeContentLoader.ReadContentAsync(TestSession.Shared, picture, DecompilationLanguage.CSharp, DecompilerOptions.Default,
            DocumentNoteTexts.For(Locale.Ja), firstGridRows: 1).GetValueAsync();

        var english = Assert.IsType<NodeContent.Code>(englishRead).Document;
        var japanese = Assert.IsType<NodeContent.Code>(japaneseRead).Document;
        Assert.Equal("// The BMP is cut short.", english.Lines[0].Text);
        Assert.Equal("// BMP のデータが途中で切れています。", japanese.Lines[0].Text);
        Assert.Equal(english.Lines.Skip(1).Select(static l => l.Text), japanese.Lines.Skip(1).Select(static l => l.Text));
    }

    [Theory]
    [MemberData(nameof(LocalizationTests.AllLocales), MemberType = typeof(LocalizationTests))]
    public void Every_note_is_one_line_in_every_language_so_a_switch_moves_no_line_under_it(Locale locale)
    {
        var notes = DocumentNoteTexts.For(locale);
        DocumentNote[] all =
        [
            new DocumentNote.ReferenceUnreadable { Reference = "R", File = "F", Why = CannotOpen("first" + Environment.NewLine + "second"), Module = "M" },
            new DocumentNote.ReferenceUnreadable { Reference = "R", File = "F", Why = NotAssembly(), Module = "M" },
            new DocumentNote.ReferenceNotFound("R"),
            new DocumentNote.DecompileFailed("N"),
            new DocumentNote.FailureCause(DocumentNote.OneLine("first" + Environment.NewLine + "second")),
            new DocumentNote.AssemblyUnreadable { File = "F", Error = CannotOpen("W") },
            new DocumentNote.AssemblyUnreadable { File = "F", Error = NotAssembly() },
            new DocumentNote.ReferenceAssembly(),
            new DocumentNote.ModuleKind(true),
            new DocumentNote.ModuleKind(false),
            new DocumentNote.Calls { Entry = "E", ModuleName = "M" },
            new DocumentNote.Resolution(null),
            new DocumentNote.Resolution("P"),
            new DocumentNote.Truncated(65536),
            new DocumentNote.PictureUnreadable(new PictureFault.CutShort("BMP")),
            new DocumentNote.PictureUnreadable(new PictureFault.PixelsCutShort("PNG")),
            new DocumentNote.PictureUnreadable(new PictureFault.SizeOutOfRange { Width = 65535, Height = 65535 }),
            new DocumentNote.PictureUnreadable(new PictureFault.PaletteIndexOutOfRange()),
            new DocumentNote.PictureUnreadable(new PictureFault.CompressionNotRead()),
            new DocumentNote.PictureUnreadable(new PictureFault.BitDepthNotRead(2)),
            new DocumentNote.PictureUnreadable(new PictureFault.NoPicture("GIF")),
            new DocumentNote.PictureUnreadable(new PictureFault.CodeSizeOutOfRange()),
            new DocumentNote.PictureUnreadable(new PictureFault.ColorTypeUnknown("5")),
            new DocumentNote.PictureUnreadable(new PictureFault.NotAPicture()),
            new DocumentNote.PictureUnreadable(new PictureFault.LibraryFailure("first" + Environment.NewLine + "second")),
            new DocumentNote.ArchitectureOf(new ModuleArchitecture { Platform = "AnyCPU", PreferredBits = null }),
            new DocumentNote.ArchitectureOf(new ModuleArchitecture { Platform = "AnyCPU", PreferredBits = 64 }),
            new DocumentNote.TargetFramework("T"),
            new DocumentNote.EntryPoint { Method = "Main", Target = new CodeReference.Label { Name = "Main", Key = 0, Scope = null } },
            new DocumentNote.ForwardedTo("D"),
        ];

        Assert.All(all, note => Assert.DoesNotMatch("[\r\n\u0085\u2028\u2029]", note.Words(notes)));
    }

    [Theory]
    [MemberData(nameof(LocalizationTests.AllLocales), MemberType = typeof(LocalizationTests))]
    public void Three_forwarded_destinations_a_line_break_a_space_and_a_backslash_and_n_are_three_different_one_line_notes(Locale locale)
    {
        var notes = DocumentNoteTexts.For(locale);
        const string Code = "public class Cart { }";

        var broken = DrawForwarded("Shop\nclass Injected { }", Code, notes);
        var spaced = DrawForwarded("Shop class Injected { }", Code, notes);
        var escaped = DrawForwarded(@"Shop\nclass Injected { }", Code, notes);

        foreach (var drawn in new[] { broken, spaced, escaped })
        {
            Assert.Equal(2, drawn.Lines.Length);
            Assert.StartsWith("Cart // ", drawn.Lines[0].Text);
            Assert.Equal(Code, drawn.Lines[1].Text);
        }

        Assert.Contains(@"""Shop\nclass Injected { }""", broken.Lines[0].Text);
        Assert.Contains(@"Shop\nclass Injected { }", escaped.Lines[0].Text);
        Assert.DoesNotContain(@"""Shop\nclass", escaped.Lines[0].Text);
        Assert.Equal(3, new[] { broken, spaced, escaped }.Select(d => d.Lines[0].Text).Distinct().Count());
        Assert.All(new[] { "\t", "\v", "\r\n" }, value => Assert.DoesNotMatch("[\r\n\t\v]", new DocumentNote.ForwardedTo("Shop" + value + "Cart").Words(notes)));
    }

    // A forwarded type's line, its note saying where it went, then a line of code under it.
    private static DecompiledDocument DrawForwarded(string destination, string code, IDocumentNotes notes)
    {
        var builder = new DocumentBuilder { Language = DecompilationLanguage.CSharp };
        builder.Write("Cart", SpanKind.Type, null);
        builder.WriteNote(new DocumentNote.ForwardedTo(destination), notes);
        builder.WriteLine();
        builder.Write(code);
        builder.WriteLine();
        return builder.BuildDocument("Shop");
    }

    [Fact]
    public async Task A_bookmark_on_a_line_under_a_note_stays_on_the_same_code_after_a_language_switch()
    {
        using var temp = new TempDir();
        var sample = temp.Copy(PathsSamplePath, "PathsSample.dll");
        temp.Write("PathsLibrary.dll", "no assembly");
        var app = new AppDriver();
        app.Open([AssemblyPath.Of(sample)]);
        app.Settle();
        var assembly = app.AppState.ListSession.Assemblies.Assemblies.Single(a => a.FilePath.FileName == "PathsSample.dll");
        var overloads = await assembly.ReadRootAsync().GetChildAsync("PathsSample", "Overloads");
        app.Show(overloads, frames: 2);
        var tab = Assert.Single(app.AppState.ListSession.Navigation.OpenTabs);
        Assert.NotNull(tab.Node);
        var made = tab.Document;
        Assert.Equal(0, Assert.Single(made.Notes).Line);
        var line = made.GetPosition("Take(").Line;
        var toggle = Bookmarks.ToggleBookmark(app.AppState, tab, line, Names.Bookmark);
        Assert.IsType<BookmarkToggle.Added>(toggle);
        var code = made.Lines[line].Text;

        app.AppState.Locale = Locale.Ja;
        app.Run(2);

        var shown = app.Ui.Documents.GetView(tab).Code.Layout.Document;
        Assert.NotNull(shown);
        Assert.NotEqual(made.Lines[0].Text, shown.Lines[0].Text);
        Assert.Equal(made.Lines.Length, shown.Lines.Length);
        Assert.Equal([line], app.AppState.Bookmarks.ListMarkedLines(tab.Document, tab.Node).Keys);
        Assert.Equal(code, shown.Lines[line].Text);

        app.Driver.InTurn(() => Commands.StartGoingToBookmark(app.AppState, app.AppState.Bookmarks.Items[0]));
        app.Settle(4);

        Assert.Equal(line, app.Ui.Documents.GetView(tab).Code.Reveal.Flash?.Line);
        Assert.Equal(code, app.Ui.Documents.GetView(tab).Code.Layout.Document?.Lines[line].Text);
    }

    [Fact]
    public void The_core_tests_english_notes_are_the_apps_english_catalog()
    {
        IDocumentNotes app = DocumentNoteTexts.For(Locale.En);
        IDocumentNotes tests = EnglishNotes.Instance;

        Assert.Equal(app.ReferenceUnreadable("R", "F", "W", "M"), tests.ReferenceUnreadable("R", "F", "W", "M"));
        Assert.Equal(app.ReferenceNotFound("R"), tests.ReferenceNotFound("R"));
        Assert.Equal(app.DecompileFailed("N"), tests.DecompileFailed("N"));
        Assert.Equal(app.FailureCause("W"), tests.FailureCause("W"));
        Assert.Equal(app.AssemblyUnreadable("F", null), tests.AssemblyUnreadable("F", null));
        Assert.Equal(app.AssemblyUnreadable("F", "W"), tests.AssemblyUnreadable("F", "W"));
        Assert.Equal(app.DescribeUnopened(CannotOpen("W")), tests.DescribeUnopened(CannotOpen("W")));
        Assert.Equal(app.DescribeUnopened(NotAssembly()), tests.DescribeUnopened(NotAssembly()));
        Assert.Equal(app.ReferenceAssembly(), tests.ReferenceAssembly());
        Assert.Equal(app.ModuleWithMetadata(), tests.ModuleWithMetadata());
        Assert.Equal(app.ModuleWithoutMetadata(), tests.ModuleWithoutMetadata());
        Assert.Equal(app.Calls("E", "M"), tests.Calls("E", "M"));
        Assert.Equal(app.ReferenceNotResolved(), tests.ReferenceNotResolved());
        Assert.Equal(app.ResolvesTo("P"), tests.ResolvesTo("P"));
        Assert.Equal(app.Truncated(65536), tests.Truncated(65536));
        Assert.Equal(app.Architecture(new ModuleArchitecture { Platform = "A", PreferredBits = null }), tests.Architecture(new ModuleArchitecture { Platform = "A", PreferredBits = null }));
        Assert.Equal(app.Architecture(new ModuleArchitecture { Platform = "A", PreferredBits = 64 }), tests.Architecture(new ModuleArchitecture { Platform = "A", PreferredBits = 64 }));
        Assert.Equal(app.TargetFramework("T"), tests.TargetFramework("T"));
        Assert.Equal(app.EntryPoint("M"), tests.EntryPoint("M"));
        Assert.Equal(app.ForwardedTo("D"), tests.ForwardedTo("D"));
    }

    [Fact]
    public void A_failures_cause_is_said_by_its_message_in_the_apps_words_with_no_exception_type_or_stack_frame()
    {
        var notes = DocumentNoteTexts.For(Locale.Ja);

        Assert.Equal("原因: Bad IL range.", new DocumentNote.FailureCause("Bad IL range.").Words(notes));
        Assert.Equal(Strings.For(Locale.Ja).CauseModuleUnreadableSentence(name: "Shop.dll", why: "Access denied."),
            new DocumentNote.AssemblyUnreadable { File = "Shop.dll", Error = CannotOpen("Access denied.") }.Words(notes));
        Assert.Equal(Strings.For(Locale.Ja).ErrorFileDamaged("Shop.dll"), new DocumentNote.AssemblyUnreadable { File = "Shop.dll", Error = NotAssembly() }.Words(notes));
    }

    [Fact]
    public void A_file_that_cannot_be_opened_is_said_in_a_note_from_its_cause_in_the_apps_language()
    {
        var notes = DocumentNoteTexts.For(Locale.Ja);
        var japanese = Strings.For(Locale.Ja);
        var bundle = new BundleEntryException(new BundleEntryFault.Outside { Entry = "Shop.dll" }, "The bundle's entry 'Shop.dll' cannot be read: it starts at -1.");
        var inBundle = new AssemblyLoadException(ShopPath, new AssemblyLoadFault.CannotOpen(), "Cannot open.", bundle);
        var package = AssemblyPath.Of(Path.Combine(Path.GetTempPath(), "Shop", "Shop.exe"));
        var notInPackage = new AssemblyLoadException(ShopPath, new AssemblyLoadFault.NotInPackage { Package = package, FullName = "Shop.dll" }, "Holds no Shop.dll.",
            new FileNotFoundException("Holds no Shop.dll."));
        var bundleWhy = japanese.CauseBundleEntryOutsideSentence(entry: "Shop.dll");

        Assert.Equal(japanese.CauseModuleUnreadableSentence(name: "Shop.dll", why: bundleWhy),
            new DocumentNote.AssemblyUnreadable { File = "Shop.dll", Error = inBundle }.Words(notes));
        Assert.Equal(japanese.CauseModuleUnreadableSentence(name: "Shop.dll", why: japanese.CauseAssemblyNotInPackageSentence(package: package.Text, name: "Shop.dll")),
            new DocumentNote.AssemblyUnreadable { File = "Shop.dll", Error = notInPackage }.Words(notes));
        Assert.Equal(japanese.CauseModuleUnreadableNote(reference: "Shop", file: "Shop.dll", why: japanese.CauseAssemblyNotDotnetSentence(file: ShopPath.Text), module: "App.dll"),
            new DocumentNote.ReferenceUnreadable { Reference = "Shop", File = "Shop.dll", Why = NotAssembly(), Module = "App.dll" }.Words(notes));
    }

    private static readonly AssemblyPath ShopPath = AssemblyPath.Of(Path.Combine(Path.GetTempPath(), "Shop", "Shop.dll"));

    // A file the system or the library refused, saying why.
    private static AssemblyLoadException CannotOpen(string why)
    {
        return new AssemblyLoadException(ShopPath, new AssemblyLoadFault.CannotOpen(), $"Cannot open: {why}", new IOException(why));
    }

    // A file that is an image with no .NET metadata.
    private static AssemblyLoadException NotAssembly()
    {
        return new AssemblyLoadException(ShopPath, new AssemblyLoadFault.NotAssembly(), "Not a .NET assembly.");
    }

    [Fact]
    public void An_architecture_names_its_platform_as_it_is_and_says_the_preferred_bitness_in_the_apps_language()
    {
        var preferring = new ModuleArchitecture { Platform = "AnyCPU", PreferredBits = 64 };
        var stating = new ModuleArchitecture { Platform = "Amd64", PreferredBits = null };

        Assert.Equal("Architecture: AnyCPU (64-bit preferred)", DocumentNoteTexts.For(Locale.En).Architecture(preferring));
        Assert.Equal("アーキテクチャ: AnyCPU（64 ビット優先）", DocumentNoteTexts.For(Locale.Ja).Architecture(preferring));
        Assert.Equal("アーキテクチャ: Amd64", DocumentNoteTexts.For(Locale.Ja).Architecture(stating));
    }

    private static byte[] Picture(string name)
    {
        using var stream = typeof(DocumentNoteTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"No resource is embedded as '{name}'.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}

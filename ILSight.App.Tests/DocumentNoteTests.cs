using ILSight.Core;

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
    public void The_core_tests_english_notes_are_the_apps_english_catalog()
    {
        IDocumentNotes app = DocumentNoteTexts.For(Locale.En);
        IDocumentNotes tests = EnglishNotes.Instance;

        Assert.Equal(app.ReferenceUnreadable("R", "F", "W", "M"), tests.ReferenceUnreadable("R", "F", "W", "M"));
        Assert.Equal(app.ReferenceNotFound("R"), tests.ReferenceNotFound("R"));
        Assert.Equal(app.DecompileFailed("N"), tests.DecompileFailed("N"));
        Assert.Equal(app.ReferenceAssembly(), tests.ReferenceAssembly());
        Assert.Equal(app.ModuleWithMetadata(), tests.ModuleWithMetadata());
        Assert.Equal(app.ModuleWithoutMetadata(), tests.ModuleWithoutMetadata());
        Assert.Equal(app.Calls("E", "M"), tests.Calls("E", "M"));
        Assert.Equal(app.ReferenceNotResolved(), tests.ReferenceNotResolved());
        Assert.Equal(app.ResolvesTo("P"), tests.ResolvesTo("P"));
        Assert.Equal(app.Truncated(65536), tests.Truncated(65536));
        Assert.Equal(app.Architecture("A"), tests.Architecture("A"));
        Assert.Equal(app.TargetFramework("T"), tests.TargetFramework("T"));
        Assert.Equal(app.EntryPoint("M"), tests.EntryPoint("M"));
        Assert.Equal(app.ForwardedTo("D"), tests.ForwardedTo("D"));
    }
}

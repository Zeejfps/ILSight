using ILSight.Core;

namespace ILSight.Tests;

public sealed class DocumentRewordTests
{
    [Fact]
    public async Task A_documents_notes_are_said_again_in_other_words_from_their_causes_its_code_and_links_unchanged()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var root = await list.Open(Trees.SampleAppPath).ReadRootAsync();
        var made = await root.DecompileAsync(DecompilationLanguage.CSharp);
        var entry = made.LineContaining("// Entry point: ");
        var method = Assert.Single(made.Lines[entry].Spans.ToArray(), static s => s.Reference is not null);

        var reworded = Documents.Reword(made, new OtherWords());

        Assert.Same(made, reworded.AsMade);
        Assert.Same(made, Documents.Reword(made, EnglishNotes.Instance));
        Assert.Equal(made.Lines.Length, reworded.Lines.Length);
        var noted = made.Notes.Select(static n => n.Line).ToHashSet();
        Assert.Contains(entry, noted);
        for (var i = 0; i < made.Lines.Length; i++)
        {
            if (!noted.Contains(i))
            {
                Assert.Equal(made.Lines[i].Text, reworded.Lines[i].Text);
                Assert.Equal(made.Lines[i].Spans.ToArray(), reworded.Lines[i].Spans.ToArray());
            }
        }

        var name = made.Lines[entry].Text.Substring(method.Start, method.Length);
        Assert.Equal($"// {name} is where it starts", reworded.Lines[entry].Text);
        var link = reworded.Lines[entry].SpanAt(name);
        Assert.Equal(method.Reference, link.Reference);
        Assert.Equal(SpanKind.Method, link.Kind);
        Assert.Equal(made.Folds, reworded.Folds);
        Assert.Same(made, Documents.Reword(reworded, EnglishNotes.Instance).AsMade);
        Assert.Equal(made.Lines.Select(static l => l.Text), Documents.Reword(reworded, EnglishNotes.Instance).Lines.Select(static l => l.Text));
    }
}

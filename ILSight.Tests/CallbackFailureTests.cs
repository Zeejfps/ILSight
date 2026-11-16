using ICSharpCode.Decompiler;
using ILSight.Core;

namespace ILSight.Tests;

/// <summary>The app's code the decompiler calls back while it reads (the document builder) is the app's: a bug in it
/// reaches the edge, never read as the decompiler's failure on the node (CD4).</summary>
public sealed class CallbackFailureTests
{
    [Fact]
    public async Task A_bug_in_the_builders_work_while_the_decompiler_writes_reaches_the_edge_not_a_document_saying_the_decompiler_failed()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var root = await list.Open(Trees.TestAssemblyPath).ReadRootAsync();
        var notes = new BrokenArchitectureNotes();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NodeContentLoader.DecompileAsync(TestSession.Shared, root, DecompilationLanguage.CSharp, DecompilerOptions.Default, notes).GetValueAsync());

        Assert.Equal(BrokenArchitectureNotes.Message, thrown.Message);
        var read = await root.DecompileAsync(DecompilationLanguage.CSharp);
        Assert.Null(read.Error);
    }

    [Fact]
    public void A_member_of_the_builder_that_throws_marks_it_failed_for_the_rest_of_the_read_and_one_that_returns_does_not()
    {
        var builder = new DocumentBuilder { Language = DecompilationLanguage.CSharp };
        ITextOutput output = builder;
        var note = new DocumentNote.ArchitectureOf(new ModuleArchitecture { Platform = "AnyCPU", PreferredBits = 64 });

        output.Write("public class Cart");
        output.WriteLine();
        Assert.False(builder.Failed);

        Assert.Throws<InvalidOperationException>(() => builder.WriteNote(note, new BrokenArchitectureNotes()));

        Assert.True(builder.Failed);
        output.WriteLine();
        Assert.True(builder.Failed);
    }

    [Fact]
    public void A_builder_of_a_methods_CSharp_sharing_the_reads_mark_marks_the_whole_read_failed()
    {
        var read = new DocumentBuilder { Language = DecompilationLanguage.IL };
        var methodCSharp = new DocumentBuilder { Language = DecompilationLanguage.CSharp, Callbacks = read.Callbacks };

        var note = new DocumentNote.ArchitectureOf(new ModuleArchitecture { Platform = "AnyCPU", PreferredBits = 64 });

        Assert.Throws<InvalidOperationException>(() => methodCSharp.WriteNote(note, new BrokenArchitectureNotes()));

        Assert.True(read.Failed);
    }
}

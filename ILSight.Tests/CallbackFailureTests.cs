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

    // What a callback reads of the library's objects is the read's (CF9): a lazy decode that throws there is the data's
    // failure, the builder and the resolver left unmarked.
    [Fact]
    public void A_type_or_member_the_library_fails_reading_while_the_builder_writes_it_is_the_reads_failure_leaving_the_builder_unmarked()
    {
        var builder = new DocumentBuilder { Language = DecompilationLanguage.CSharp };
        ITextOutput output = builder;

        Assert.Throws<BadImageFormatException>(() => output.WriteReference(UnreadableLibraryObject.Create<ICSharpCode.Decompiler.TypeSystem.IType>(), "Cart"));
        Assert.Throws<BadImageFormatException>(() => output.WriteReference(UnreadableLibraryObject.Create<ICSharpCode.Decompiler.TypeSystem.IMember>(), "Add"));
        Assert.Throws<BadImageFormatException>(() => builder.MarkStatement(UnreadableLibraryObject.Create<ICSharpCode.Decompiler.TypeSystem.IMethod>(), 0));

        Assert.False(builder.Failed);
    }

    [Fact]
    public void A_reference_row_the_library_fails_reading_when_it_asks_the_resolver_is_the_reads_failure_leaving_the_resolver_unmarked()
    {
        var callbacks = new CallbackFailure();
        using var reading = LoadedAssemblies.BeginReading();
        var resolver = LoadedAssemblies.CreateResolver(reading, Trees.TestAssembly, callbacks);

        Assert.Throws<BadImageFormatException>(() => resolver.Resolve(UnreadableLibraryObject.Create<ICSharpCode.Decompiler.Metadata.IAssemblyReference>()));

        Assert.False(callbacks.Failed);
    }
}

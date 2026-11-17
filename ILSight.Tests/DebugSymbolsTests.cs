using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using ILSight.Core;
using ILSight.Tests.Fixtures;

namespace ILSight.Tests;

public sealed class DebugSymbolsTests
{
    [Fact]
    public void The_pdb_beside_the_assembly_is_found_and_read_once()
    {
        var assembly = Trees.TestAssembly;

        var symbols = LoadedAssemblies.EnsureDebugSymbols(assembly);

        Assert.NotNull(symbols);
        Assert.Equal(Path.ChangeExtension(Trees.TestAssemblyPath, ".pdb"), symbols.SourceFileName);
        var symbolsAgain = LoadedAssemblies.EnsureDebugSymbols(assembly);
        Assert.Same(symbols, symbolsAgain);
    }

    [Fact]
    public void Locals_keep_the_tuple_names_and_dynamic_flags_their_signatures_cannot()
    {
        var symbols = LoadedAssemblies.EnsureDebugSymbols(Trees.TestAssembly);
        Assert.NotNull(symbols);
        var callbacks = new CallbackFailure();
        var reader = new DebugSymbolsReader(symbols, callbacks);

        Assert.True(reader.TryGetExtraTypeInfo(CircleMethod(nameof(Circle.Spread)), 0, out var tuple));
        Assert.True(reader.TryGetExtraTypeInfo(CircleMethod(nameof(Circle.Echo)), 0, out var dynamic));
        Assert.False(callbacks.Failed);

        Assert.Equal(["Low", "High"], tuple.TupleElementNames);
        Assert.Null(tuple.DynamicFlags);
        Assert.NotNull(dynamic.DynamicFlags);
        Assert.True(dynamic.DynamicFlags[0]);
    }

    [Fact]
    public void A_local_without_extra_type_info_has_none()
    {
        var symbols = LoadedAssemblies.EnsureDebugSymbols(Trees.TestAssembly);
        Assert.NotNull(symbols);
        var reader = new DebugSymbolsReader(symbols, new CallbackFailure());

        Assert.False(reader.TryGetExtraTypeInfo(CircleMethod(nameof(Circle.Grow)), 0, out _));
        Assert.True(reader.TryGetName(CircleMethod(nameof(Circle.Grow)), 1, out var name));
        Assert.Equal("step", name);
    }

    // The reader's own decoding of a tuple names blob says bad data as a value, which the reader throws as
    // InvalidDataException outside its count (CJ11, CF4).
    [Fact]
    public void A_tuple_names_blob_is_decoded_name_by_name_and_one_whose_last_name_has_no_terminator_is_bad_data()
    {
        var names = DebugSymbolsReader.DecodeTupleElementNames("Low\0\0High\0"u8.ToArray());
        Assert.NotNull(names);
        Assert.Equal<IEnumerable<string?>>(["Low", null, "High"], names);
        Assert.Empty(DebugSymbolsReader.DecodeTupleElementNames([]) ?? ["missing"]);
        Assert.Null(DebugSymbolsReader.DecodeTupleElementNames("Low\0High"u8.ToArray()));
    }

    [Fact]
    public void A_picked_pdb_is_checked_against_the_build_the_assembly_names()
    {
        using var temp = new TempDir();
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var test = list.OpenModule(Trees.TestAssemblyPath);
        var sample = list.OpenModule(Trees.SampleAppPath);
        var windows = temp.Write("windows.pdb", Encoding.ASCII.GetBytes("Microsoft C/C++ MSF 7.00\r\n\u001aDS\0\0\0"));
        var text = temp.Write("notes.pdb", "not a pdb");

        Assert.Equal(PdbCheck.Matches, PdbFiles.Check(test, TestPdbPath));
        Assert.Equal(PdbCheck.OtherBuild, PdbFiles.Check(sample, TestPdbPath));
        Assert.Equal(PdbCheck.WindowsPdb, PdbFiles.Check(test, windows));
        Assert.Equal(PdbCheck.Unreadable, PdbFiles.Check(test, text));
        Assert.Equal(PdbCheck.Unreadable, PdbFiles.Check(test, temp.PathOf("missing.pdb")));
    }

    [Fact]
    public async Task An_assembly_reads_the_pdb_picked_for_it_once_it_is_opened_again()
    {
        using var temp = new TempDir();
        var copy = temp.Copy(Trees.TestAssemblyPath, Path.GetFileName(Trees.TestAssemblyPath));
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var module = list.OpenModule(copy);
        var before = LoadedAssemblies.EnsureDebugSymbols(module);

        list.Pdbs.Pick(AssemblyPath.Of(copy), TestPdbPath);
        var removed = list.CloseAll();
        LoadedAssemblies.Release(removed);
        var after = list.OpenModule(copy);

        Assert.Null(before);
        var symbols = LoadedAssemblies.EnsureDebugSymbols(after);
        Assert.NotNull(symbols);
        Assert.Equal(TestPdbPath, symbols.SourceFileName);
        var reader = new DebugSymbolsReader(symbols, new CallbackFailure());
        Assert.True(reader.TryGetName(CircleMethod(nameof(Circle.Grow)), 1, out var name));
        Assert.Equal("step", name);
        var afterRoot = await after.ReadRootAsync();
        var afterRootChildren = await afterRoot.ListChildrenAsync();
        Assert.Contains(afterRootChildren, c => c.Text == "Debug Metadata (From portable PDB)");
    }

    [Fact]
    public void Closing_an_assembly_forgets_the_pdb_picked_for_it()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var test = list.Open(Trees.TestAssemblyPath);
        list.Pdbs.Pick(test.FilePath, TestPdbPath);

        var removed = list.Close(test);
        LoadedAssemblies.Release(removed);

        Assert.Empty(list.Pdbs.All);
    }

    private static string TestPdbPath => Path.ChangeExtension(Trees.TestAssemblyPath, ".pdb");

    private static MethodDefinitionHandle CircleMethod(string name)
    {
        var token = (typeof(Circle).GetMethod(name) ?? throw new MissingMethodException(nameof(Circle), name)).MetadataToken;
        return (MethodDefinitionHandle)MetadataTokens.EntityHandle(token);
    }
}

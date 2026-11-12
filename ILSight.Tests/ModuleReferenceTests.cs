using ILSight.Core;

namespace ILSight.Tests;

public sealed class ModuleReferenceTests
{
    /// <summary>The References folder of the runtime's assembly <paramref name="name"/>.</summary>
    private static async Task<BrowseNode> ReferencesOfAsync(string name)
    {
        var path = RuntimeAssemblies.Find(TestRuntime.Installed, name);
        Assert.NotNull(path);
        var assembly = new AssemblyList { Runtime = TestRuntime.Installed }.Open(path);
        return await assembly.ReadRootAsync().GetChildAsync("References");
    }

    /// <summary>The References folder of System.Private.CoreLib, which calls into native modules.</summary>
    private static async Task<BrowseNode> CoreLibReferencesAsync()
    {
        return await ReferencesOfAsync("System.Private.CoreLib");
    }

    [Fact]
    public async Task The_references_list_the_modules_an_assembly_calls_into_sorted_by_name()
    {
        var modules = (await CoreLibReferencesAsync().ListChildrenAsync())
            .Where(c => c.Subject is NodeSubject.ModuleReference)
            .Select(c => c.Text)
            .ToList();

        Assert.Contains("QCall", modules);
        Assert.Equal(modules.Order(NaturalStringComparer.Instance), modules);
    }

    [Fact]
    public async Task Module_references_come_after_the_assembly_references()
    {
        var modules = (await ReferencesOfAsync("System.IO.Compression").ListChildrenAsync())
            .Select(static c => c.Subject is NodeSubject.ModuleReference)
            .ToList();

        Assert.Contains(false, modules);
        Assert.Contains(true, modules);
        Assert.Equal(modules.Order(), modules);
    }

    [Fact]
    public async Task A_module_reference_reads_as_its_name_and_a_native_library_when_the_assembly_lists_no_such_file()
    {
        var qcall = await CoreLibReferencesAsync().GetChildAsync("QCall");

        var text = Documents.GetText(Assert.IsType<NodeContent.Code>(await qcall.ReadContentAsync()).Document);

        Assert.Contains("// QCall", text);
        Assert.Contains("A native library", text);
    }

    [Fact]
    public async Task The_references_folder_reads_as_its_assemblies_then_its_modules()
    {
        var text = Documents.GetText(Assert.IsType<NodeContent.Code>(await CoreLibReferencesAsync().ReadContentAsync()).Document);

        Assert.Contains("// QCall", text);
    }

    [Fact]
    public async Task A_module_lists_the_methods_imported_from_it_with_the_type_declaring_each()
    {
        var (_, assembly) = Trees.OpenTestAssembly();

        var answer = await assembly.ReadRootAsync().GetChildAsync("References", "fixture-native").GetChildAsync("Answer() : int");

        Assert.IsType<NodeSubject.ImportedMethod>(answer.Subject);
        Assert.Equal("ILSight.Tests.Fixtures.NativeImports", Assert.IsType<NodeSubject.ImportedMethod>(answer.Subject).Value.DeclaringTypeName);
    }

    [Fact]
    public async Task An_imported_method_reads_as_the_function_it_calls_and_goes_to_the_method()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var answer = await assembly.ReadRootAsync().GetChildAsync("References", "fixture-native", "Answer() : int");

        var text = Documents.GetText(Assert.IsType<NodeContent.Code>(await answer.ReadContentAsync()).Document);
        var definition = await list.FindNodeAsync(answer.Destination);

        Assert.Contains("Calls fixture_answer in fixture-native", text);
        Assert.Same(await assembly.FixturesAsync().GetChildAsync("NativeImports", "Answer() : int"), definition);
    }

    [Fact]
    public async Task A_stub_generated_for_an_import_is_named_and_goes_as_the_method_declaring_it()
    {
        var qcall = await CoreLibReferencesAsync().GetChildAsync("QCall");
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var coreLibPath = RuntimeAssemblies.Find(TestRuntime.Installed, "System.Private.CoreLib");
        Assert.NotNull(coreLibPath);
        var coreLib = list.Open(coreLibPath);
        var imports = await coreLib.ReadRootAsync().GetChildAsync("References", "QCall").ListChildrenAsync();

        // GC._Collect is a generated stub on every system; most QCalls are not, or not everywhere.
        var collect = imports.First(i => i.Text.StartsWith("_Collect(", StringComparison.Ordinal));
        var definition = await list.FindNodeAsync(collect.Destination);
        Assert.NotNull(definition);

        Assert.DoesNotContain(await qcall.ListChildrenAsync(), i => i.Text.Contains(">g__", StringComparison.Ordinal));
        Assert.Equal("System.GC", Assert.IsType<NodeSubject.ImportedMethod>(collect.Subject).Value.DeclaringTypeName);
        Assert.StartsWith("_Collect(", definition.Text, StringComparison.Ordinal);
        Assert.Equal(MemberShape.Method, definition.MemberShape);
    }
}

using ILSight.Core;

namespace ILSight.Tests;

public sealed class ReferenceReferencesTests
{
    private const string Runtime = "System.Runtime";
    private const string CoreLib = "System.Private.CoreLib";

    private static async Task<BrowseNode> RuntimeReferenceAsync(LoadedAssembly assembly)
    {
        var root = await assembly.ReadRootAsync();
        return await root.GetChildAsync("References", Runtime);
    }

    [Fact]
    public async Task A_reference_lists_the_references_of_the_assembly_it_resolves_to_after_the_types_used_from_it()
    {
        var (_, assembly) = Trees.OpenTestAssembly();

        var runtimeReference = await RuntimeReferenceAsync(assembly);
        var children = await runtimeReference.ListChildrenAsync();

        Assert.IsType<NodeSubject.ReferencedTypes>(children[0].Subject);
        var coreLib = Assert.Single(children, c => c.Text == CoreLib);
        Assert.IsType<NodeSubject.AssemblyReference>(coreLib.Subject);
        Assert.Equal(Runtime, coreLib.Assembly.Name);
    }

    [Fact]
    public async Task The_assembly_a_reference_resolves_to_is_read_without_being_listed()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var runtimeReference = await RuntimeReferenceAsync(assembly);
        await runtimeReference.EnsureChildrenAsync();

        Assert.Equal([assembly], list.Listed);
    }

    [Fact]
    public async Task A_reference_of_a_reference_holds_what_that_assembly_uses_of_it()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var runtimeReference = await RuntimeReferenceAsync(assembly);
        var coreLib = await runtimeReference.GetChildAsync(CoreLib);

        var referencedTypes = await coreLib.GetChildAsync("Referenced types");
        var forwarded = await referencedTypes.ListChildrenAsync();

        Assert.Contains(forwarded, f => f.Subject is NodeSubject.ForwardedType && f.Text == "Object");
    }

    [Fact]
    public async Task A_reference_of_a_reference_is_found_again_by_its_path()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var runtimeReference = await RuntimeReferenceAsync(assembly);
        var coreLib = await runtimeReference.GetChildAsync(CoreLib);

        var found = await list.FindNodeAsync(coreLib.Path);

        Assert.Same(coreLib, found);
        Assert.Same(await assembly.ReadRootAsync(), coreLib.Root);
    }

    [Fact]
    public async Task A_reference_of_a_reference_reads_as_where_it_resolves_from_the_assembly_that_holds_it()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var runtimeReference = await RuntimeReferenceAsync(assembly);
        var coreLib = await runtimeReference.GetChildAsync(CoreLib);

        var text = Documents.GetText(Assert.IsType<NodeContent.Code>(await coreLib.ReadContentAsync()).Document);

        Assert.Contains("Resolves to", text);
        Assert.Contains($"{CoreLib}.dll", text);
    }
}

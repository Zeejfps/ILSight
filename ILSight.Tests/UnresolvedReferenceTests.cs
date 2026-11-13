using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.Tests;

/// <summary>The test assembly copied away from ILSight.Core, which it references and which then
/// resolves to nothing.</summary>
public sealed class UnresolvedReferenceTests : IDisposable
{
    private const string Core = "ILSight.Core";

    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    // Its base type is defined in ILSight.Core.
    private sealed class Detached : GridSchema;

    private (AssemblyList List, LoadedModule Assembly) OpenAlone()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        return (list, list.OpenModule(_temp.Copy(Trees.TestAssemblyPath, "ILSight.Tests.dll")));
    }

    private static async Task<BrowseNode> BaseTypesAsync(LoadedAssembly assembly)
    {
        return await assembly.ReadRootAsync().GetChildAsync("ILSight.Tests", nameof(UnresolvedReferenceTests), nameof(Detached), "Base Types");
    }

    [Fact]
    public async Task A_reference_that_resolves_to_no_assembly_lists_only_the_types_used_from_it_and_goes_nowhere()
    {
        var (list, assembly) = OpenAlone();

        var core = await assembly.ReadRootAsync().GetChildAsync("References", Core);

        Assert.IsType<NodeSubject.ReferencedTypes>(Assert.Single(await core.ListChildrenAsync()).Subject);
        Assert.Null(await list.FindNodeAsync(core.Destination));
        Assert.Contains("// Not found", Documents.GetText(await core.DecompileAsync(DecompilationLanguage.CSharp)));
    }

    [Fact]
    public async Task A_base_type_that_resolves_to_no_definition_is_listed_by_name_without_a_target()
    {
        var (_, alone) = OpenAlone();
        var (list, beside) = Trees.OpenTestAssembly();

        var unresolved = await BaseTypesAsync(alone).GetChildAsync(nameof(GridSchema));
        var resolved = await BaseTypesAsync(beside).GetChildAsync(nameof(GridSchema));

        Assert.IsType<NodeSubject.BaseType>(unresolved.Subject);
        Assert.IsType<Activation.OpenSelf>(unresolved.Activation);
        Assert.Equal(NodeAccessibility.None, unresolved.Accessibility);
        Assert.True(unresolved.IsPublicApi);
        Assert.Contains(nameof(GridSchema), Documents.GetText(await unresolved.DecompileAsync(DecompilationLanguage.CSharp)));
        Assert.Equal(NodeAccessibility.Public, resolved.Accessibility);
        Assert.Equal(nameof(GridSchema), Assert.IsAssignableFrom<BrowseNode>(await list.FindNodeAsync(resolved.Destination)).Text);
    }

    [Fact]
    public async Task A_type_used_from_an_assembly_that_does_not_resolve_is_listed_with_its_members_and_goes_nowhere()
    {
        var (list, assembly) = OpenAlone();

        var used = await assembly.ReadRootAsync().GetChildAsync("References", Core, "Referenced types", nameof(GridSchema));

        Assert.IsType<NodeSubject.TypeReference>(used.Subject);
        Assert.Equal(typeof(GridSchema).FullName, used.FullName);
        Assert.Null(await list.FindNodeAsync(used.Destination));
        Assert.NotEmpty(await used.ListChildrenAsync());
        foreach (var member in await used.ListChildrenAsync())
        {
            Assert.Null(await list.FindNodeAsync(member.Destination));
        }
    }
}

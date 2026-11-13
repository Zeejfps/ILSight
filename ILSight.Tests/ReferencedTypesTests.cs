using ILSight.Core;

namespace ILSight.Tests;

public sealed class ReferencedTypesTests
{
    private const string ListLabel = "List<>";
    private const string ListNamespace = "System.Collections.Generic";

    private static async Task<BrowseNode> UsedListAsync(LoadedAssembly assembly)
    {
        var used = new List<BrowseNode>();
        foreach (var reference in await assembly.ReadRootAsync().GetChildAsync("References").ListChildrenAsync())
        {
            var children = await reference.ListChildrenAsync();
            foreach (var types in children.Where(c => c.Subject is NodeSubject.ReferencedTypes))
            {
                used.AddRange(await types.ListChildrenAsync());
            }
        }

        return used.Single(IsList);
    }

    private static bool IsList(BrowseNode node)
    {
        if (node.Text != ListLabel)
        {
            return false;
        }

        return node.Subject is NodeSubject.TypeReference type && type.Value.Namespace == ListNamespace;
    }

    [Fact]
    public async Task A_reference_lists_the_types_used_from_it_with_the_members_used_of_each()
    {
        var (_, assembly) = Trees.OpenTestAssembly();

        var list = await UsedListAsync(assembly);

        Assert.IsType<NodeSubject.TypeReference>(list.Subject);
        Assert.Equal("System.Collections.Generic.List<>", list.FullName);
        Assert.Equal(MemberShape.Method, Assert.IsType<NodeSubject.MemberReference>((await list.GetChildAsync("Add(T) : void")).Subject).Value.Shape);
        Assert.IsType<NodeSubject.TypeReference>((await list.GetChildAsync("Enumerator")).Subject);
    }

    [Fact]
    public async Task A_used_member_reads_as_its_type_declares_it_and_an_accessor_as_its_property()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var list = await UsedListAsync(assembly);

        var current = await list.GetChildAsync("Enumerator", "Current : T");

        Assert.Equal(MemberShape.Property, Assert.IsType<NodeSubject.MemberReference>(current.Subject).Value.Shape);
        Assert.DoesNotContain(await list.ListChildrenAsync(), IsSpecialized);
    }

    private static bool IsSpecialized(BrowseNode node)
    {
        return node.Text.Contains("!0", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_used_type_goes_to_where_it_is_defined()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var definition = await list.FindNodeAsync((await UsedListAsync(assembly)).Destination);

        Assert.NotNull(definition);
        Assert.Equal(TypeShape.Class, definition.TypeShape);
        Assert.Equal("List<T>", definition.Text);
    }

    [Fact]
    public async Task The_referenced_types_read_as_each_type_with_what_is_used_of_it_indented_under_it()
    {
        var (_, assembly) = Trees.OpenTestAssembly();
        var types = (await UsedListAsync(assembly)).Parent;
        Assert.NotNull(types);

        var text = Documents.GetText(Assert.IsType<NodeContent.Code>(await types.ReadContentAsync()).Document);

        Assert.Contains("System.Collections.Generic.List<>", text);
        Assert.Contains("    Add(T) : void", text);
    }
}

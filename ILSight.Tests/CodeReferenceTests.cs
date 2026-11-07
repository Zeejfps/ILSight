using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ILSight.Core;

namespace ILSight.Tests;

public sealed class CodeReferenceTests
{
    private static readonly AssemblyPath Path = AssemblyPath.Of(System.IO.Path.GetFullPath("sample.dll"));

    [Fact]
    public void A_reference_to_a_file_s_own_assembly_goes_by_the_assembly_s_definition()
    {
        var reference = CodeReferences.CreateAssembly("Sample", Path);

        Assert.Null(reference.Referenced);
        Assert.Equal(EntityHandle.AssemblyDefinition, reference.Handle);
    }

    [Fact]
    public void A_reference_to_an_assembly_it_references_goes_by_that_reference()
    {
        var referenced = MetadataTokens.AssemblyReferenceHandle(3);

        var reference = CodeReferences.CreateAssembly("Other", Path, referenced);

        EntityHandle expected = referenced;
        Assert.Equal(referenced, reference.Referenced);
        Assert.Equal(expected, reference.Handle);
    }

    [Fact]
    public void Two_references_to_the_same_assembly_compare_equal_and_hash_alike()
    {
        var referenced = MetadataTokens.AssemblyReferenceHandle(3);

        var first = CodeReferences.CreateAssembly("Other", Path, referenced);
        var second = CodeReferences.CreateAssembly("Other", Path, referenced);

        Assert.Equal<CodeReference>(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual<CodeReference>(first, CodeReferences.CreateAssembly("Other", Path));
    }

    [Fact]
    public void An_entity_goes_by_the_handle_it_was_made_with()
    {
        var handle = MetadataTokens.EntityHandle(TableIndex.TypeDef, 7);

        var reference = CodeReferences.CreateEntity(EntityKind.Type, "Type", Path, handle);

        Assert.Equal(handle, reference.Handle);
    }
}

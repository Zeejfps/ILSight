using ILSight.Core;

namespace ILSight.Tests;

public sealed class MicrosoftDocsTests
{
    private const string ApiReference = "https://learn.microsoft.com/dotnet/api/";

    [Fact]
    public async Task Namespaces_types_and_members_open_their_pages_of_the_api_reference()
    {
        var coreLib = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.CoreLibPath);
        var coreLibRoot = await coreLib.ReadRootAsync();
        var generic = await coreLibRoot.GetChildAsync("System.Collections.Generic");
        var list = await generic.GetChildAsync("List<T>");
        var dictionary = await generic.GetChildAsync("Dictionary<TKey, TValue>");
        var children = await list.ListChildrenAsync();
        var constructors = children.Where(c => c.Text == "List()").ToList();
        var constructor = constructors.Single(c => c.IsPublicApi);
        var staticConstructor = constructors.Single(c => !c.IsPublicApi);

        Assert.Equal(ApiReference + "system.collections.generic", MicrosoftDocs.FindPage(generic));
        Assert.Equal(ApiReference + "system.collections.generic.list-1", MicrosoftDocs.FindPage(list));
        var add = await list.GetChildAsync("Add(T) : void");
        Assert.Equal(ApiReference + "system.collections.generic.list-1.add", MicrosoftDocs.FindPage(add));
        Assert.Equal(ApiReference + "system.collections.generic.list-1.-ctor", MicrosoftDocs.FindPage(constructor));
        Assert.Null(MicrosoftDocs.FindPage(staticConstructor));
        var enumerator = await dictionary.GetChildAsync("Enumerator");
        Assert.Equal(ApiReference + "system.collections.generic.dictionary-2.enumerator", MicrosoftDocs.FindPage(enumerator));
    }

    [Fact]
    public async Task A_reference_in_code_finds_the_page_of_its_definition_but_not_of_a_private_member_or_a_local()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var calculator = await assembly.CalculatorAsync();
        var document = await calculator.DecompileAsync(DecompilationLanguage.CSharp);
        var call = document.Lines[document.LineContaining("_items.Add(item);")];
        var addNode = await calculator.GetChildAsync("Add(T) : void");
        var add = await addNode.DecompileAsync(DecompilationLanguage.CSharp);
        var item = add.SpansOf().First(s => s.Text == "item").Span.Reference;

        Assert.Equal(ApiReference + "system.collections.generic.list-1.add", (await AssemblyListing.FindApiReferencePageAsync(list, Assert.IsType<CodeReference.Entity>(call.SpanAt("Add").Reference), CancellationToken.None)).GetValue().FoundOrNull);
        Assert.IsType<Lookup<string>.NotFound>((await AssemblyListing.FindApiReferencePageAsync(list, Assert.IsType<CodeReference.Entity>(call.SpanAt("_items").Reference), CancellationToken.None)).GetValue());
        Assert.IsType<CodeReference.Local>(item);
        Assert.DoesNotContain(list.Listed, a => a.Name == "System.Private.CoreLib");
    }

    [Fact]
    public async Task What_is_not_visible_outside_its_assembly_has_no_page()
    {
        var assembly = Trees.TestAssembly;
        var calculator = await assembly.CalculatorAsync();

        var items = await calculator.GetChildAsync("_items : List<T>");
        Assert.Null(MicrosoftDocs.FindPage(items));
        var root = await assembly.ReadRootAsync();
        Assert.Null(MicrosoftDocs.FindPage(root));
        var references = await root.GetChildAsync("References");
        Assert.Null(MicrosoftDocs.FindPage(references));
    }

    [Fact]
    public async Task A_member_whose_name_the_library_cannot_read_has_no_page_and_says_why_as_its_failure()
    {
        using var temp = new TempDir();
        var add = BrokenAssemblies.GetMethod(Trees.TestAssemblyPath, "Calculator`1", "Add");
        var unreadablePath = BrokenAssemblies.WriteUnreadableMethodName(temp, Trees.TestAssemblyPath, add);
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var reference = new CodeReference.Entity { Name = "Add", Kind = EntityKind.Method, FilePath = AssemblyPath.Of(unreadablePath), Target = add };

        var page = await AssemblyListing.FindApiReferencePageAsync(list, reference, CancellationToken.None);

        Assert.IsType<BadImageFormatException>(UnionAsserts.GetCase<Outcome<Lookup<string>>.Failed>(page).Error);
    }
}

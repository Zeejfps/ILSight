using ILSight.Core;

namespace ILSight.Tests;

public sealed class MicrosoftDocsTests
{
    private const string ApiReference = "https://learn.microsoft.com/dotnet/api/";

    [Fact]
    public async Task Namespaces_types_and_members_open_their_pages_of_the_api_reference()
    {
        var coreLib = new AssemblyList { Runtime = TestRuntime.Installed }.Open(Trees.CoreLibPath);
        var generic = await coreLib.ReadRootAsync().GetChildAsync("System.Collections.Generic");
        var list = await generic.GetChildAsync("List<T>");
        var dictionary = await generic.GetChildAsync("Dictionary<TKey, TValue>");
        var constructors = (await list.ListChildrenAsync()).Where(c => c.Text == "List()").ToList();
        var constructor = constructors.Single(c => c.IsPublicApi);
        var staticConstructor = constructors.Single(c => !c.IsPublicApi);

        Assert.Equal(ApiReference + "system.collections.generic", MicrosoftDocs.FindPage(generic));
        Assert.Equal(ApiReference + "system.collections.generic.list-1", MicrosoftDocs.FindPage(list));
        Assert.Equal(ApiReference + "system.collections.generic.list-1.add", MicrosoftDocs.FindPage(await list.GetChildAsync("Add(T) : void")));
        Assert.Equal(ApiReference + "system.collections.generic.list-1.-ctor", MicrosoftDocs.FindPage(constructor));
        Assert.Null(MicrosoftDocs.FindPage(staticConstructor));
        Assert.Equal(ApiReference + "system.collections.generic.dictionary-2.enumerator", MicrosoftDocs.FindPage(await dictionary.GetChildAsync("Enumerator")));
    }

    [Fact]
    public async Task A_reference_in_code_finds_the_page_of_its_definition_but_not_of_a_private_member_or_a_local()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var document = await assembly.CalculatorAsync().DecompileAsync(DecompilationLanguage.CSharp);
        var call = document.Lines[document.LineContaining("_items.Add(item);")];
        var add = await assembly.CalculatorAsync().GetChildAsync("Add(T) : void").DecompileAsync(DecompilationLanguage.CSharp);
        var item = add.SpansOf().First(s => s.Text == "item").Span.Reference;

        Assert.Equal(ApiReference + "system.collections.generic.list-1.add", (await AssemblyListing.FindApiReferencePageAsync(list, Assert.IsType<CodeReference.Entity>(call.SpanAt("Add").Reference), CancellationToken.None)).FoundOrNull);
        Assert.IsType<Lookup<string>.NotFound>(await AssemblyListing.FindApiReferencePageAsync(list, Assert.IsType<CodeReference.Entity>(call.SpanAt("_items").Reference), CancellationToken.None));
        Assert.IsType<CodeReference.Local>(item);
        Assert.DoesNotContain(list.Listed, a => a.Name == "System.Private.CoreLib");
    }

    [Fact]
    public async Task What_is_not_visible_outside_its_assembly_has_no_page()
    {
        var assembly = Trees.TestAssembly;
        var calculator = await assembly.CalculatorAsync();

        Assert.Null(MicrosoftDocs.FindPage(await calculator.GetChildAsync("_items : List<T>")));
        Assert.Null(MicrosoftDocs.FindPage(await assembly.ReadRootAsync()));
        Assert.Null(MicrosoftDocs.FindPage(await assembly.ReadRootAsync().GetChildAsync("References")));
    }
}

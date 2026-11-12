using ILSight.Core;

namespace ILSight.Tests;

public sealed class ForwardedTypeTests
{
    private static string RuntimeFile(string name)
    {
        return Path.Combine(RuntimeAssemblies.ListDirectories(TestRuntime.Installed)[0], name);
    }

    [Fact]
    public async Task A_facade_lists_the_types_it_forwards_in_their_namespaces()
    {
        var facade = new AssemblyList { Runtime = TestRuntime.Installed }.Open(RuntimeFile("System.Runtime.dll"));

        var generic = await facade.ReadRootAsync().GetChildAsync("System.Collections.Generic");
        var list = await generic.GetChildAsync("IList<>");
        var dictionary = await generic.GetChildAsync("IDictionary<,>");
        var exception = await generic.GetChildAsync("KeyNotFoundException");

        Assert.True(generic.IsPublicApi);
        Assert.IsType<NodeSubject.ForwardedType>(list.Subject);
        Assert.Equal("System.Private.CoreLib", Assert.IsType<NodeSubject.ForwardedType>(list.Subject).Value.Destination);
        Assert.Equal("System.Collections.Generic.IList<>", list.FullName);
        Assert.IsType<NodeSubject.ForwardedType>(dictionary.Subject);
        Assert.IsType<NodeSubject.ForwardedType>(exception.Subject);
        Assert.False(list.HasChildren);
    }

    [Fact]
    public async Task A_namespace_sorts_its_forwarded_types_by_name_then_by_how_many_type_parameters_they_have()
    {
        var facade = new AssemblyList { Runtime = TestRuntime.Installed }.Open(RuntimeFile("System.Runtime.dll"));

        var labels = (await facade.ReadRootAsync().GetChildAsync("System").ListChildrenAsync()).Select(c => c.Text).ToList();

        var sorted = labels
            .OrderBy(l => l.Split('<')[0], NaturalStringComparer.Instance)
            .ThenBy(l => l.Length);
        Assert.Equal(sorted, labels);
        Assert.Equal(["Action", "Action<>", "Action<,>"], labels.Where(l => l.StartsWith("Action")).Take(3));
    }

    [Fact]
    public async Task A_forwarded_type_goes_to_its_definition_however_many_forwards_away()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var netstandard = list.Open(RuntimeFile("netstandard.dll"));
        var forwarded = await netstandard.ReadRootAsync().GetChildAsync("System.Collections.Generic").GetChildAsync("List<>");

        var definition = await list.FindNodeAsync(forwarded.Destination);

        Assert.Equal("System.Collections", Assert.IsType<NodeSubject.ForwardedType>(forwarded.Subject).Value.Destination);
        Assert.NotNull(definition);
        Assert.Equal(TypeShape.Class, definition.TypeShape);
        Assert.Equal("List<T>", definition.Text);
        Assert.Equal("System.Private.CoreLib", definition.Assembly.Name);
        Assert.Contains(definition.Assembly, list.Assemblies);
    }

    [Fact]
    public async Task A_forwarded_type_reads_as_where_it_is_forwarded_to_with_a_link_to_its_definition()
    {
        var facade = new AssemblyList { Runtime = TestRuntime.Installed }.Open(RuntimeFile("System.Runtime.dll"));
        var forwarded = await facade.ReadRootAsync().GetChildAsync("System.Collections.Generic").GetChildAsync("IList<>");

        var document = await forwarded.DecompileAsync(DecompilationLanguage.CSharp);
        var namespaceNode = forwarded.Parent;
        Assert.NotNull(namespaceNode);
        var namespaceDocument = await namespaceNode.DecompileAsync(DecompilationLanguage.CSharp);

        Assert.Contains("// Forwarded to System.Private.CoreLib", Documents.GetText(document));
        Assert.Equal<CodeReference?>(forwarded.Destination, document.Lines[document.LineContaining("System.Collections.Generic.IList<>")].SpanAt("System.Collections.Generic.IList<>").Reference);
        Assert.Contains("IList<> // Forwarded to System.Private.CoreLib", Documents.GetText(namespaceDocument));
    }
}

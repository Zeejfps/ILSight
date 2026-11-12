using ILSight.Core;

namespace ILSight.Tests;

public sealed class ILSpyLayoutTests
{
    [Fact]
    public async Task A_type_opens_with_ILSpys_comments_and_imports_its_namespace_instead_of_declaring_it()
    {
        var document = await Trees.CalculatorDocumentAsync(DecompilationLanguage.CSharp);

        Assert.Equal("// " + Trees.TestAssembly.FullName, document.Lines[0].Text);
        Assert.Equal("// " + Trees.FixturesNamespace + ".Calculator<T>", document.Lines[1].Text);
        Assert.Contains(document.Lines, l => l.Text == "using " + Trees.FixturesNamespace + ";");
        Assert.DoesNotContain(document.Lines, l => l.Text.StartsWith("namespace "));
    }

    [Fact]
    public async Task A_member_opens_with_the_assembly_and_its_declaring_type()
    {
        var assembly = Trees.TestAssembly;

        var document = await assembly.CalculatorAsync().GetChildAsync("Add(T) : void").DecompileAsync(DecompilationLanguage.CSharp);

        Assert.Equal("// " + assembly.FullName, document.Lines[0].Text);
        Assert.Equal("// " + Trees.FixturesNamespace + ".Calculator<T>", document.Lines[1].Text);
    }

    [Fact]
    public async Task A_constructor_shows_with_the_fields_it_initializes_and_a_field_shows_alone()
    {
        var assembly = Trees.TestAssembly;
        var calculator = await assembly.CalculatorAsync();

        var members = await calculator.ListChildrenAsync();
        var ctor = Documents.GetText(await members.Single(c => c.Text.StartsWith("Calculator()")).DecompileAsync(DecompilationLanguage.CSharp));
        var field = Documents.GetText(await calculator.GetChildAsync("_items : List<T>").DecompileAsync(DecompilationLanguage.CSharp));

        Assert.Contains("_items = new List<T>()", ctor);
        Assert.Contains("public Calculator()", ctor);
        Assert.DoesNotContain("public void Add", ctor);
        Assert.Contains("private readonly List<T> _items", field);
        Assert.DoesNotContain("public Calculator()", field);
        Assert.DoesNotContain("Changed", field);
    }
}

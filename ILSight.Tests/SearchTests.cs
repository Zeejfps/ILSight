using System.Diagnostics;
using ILSight.Core;

namespace ILSight.Tests;

[Collection(nameof(TimedTests))]
public sealed class SearchTests
{
    [Fact]
    public async Task Types_are_found_by_case_insensitive_substring()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var results = await CollectAsync(list.SearchAsync("calcul", SearchKinds.Types));

        var calculator = Assert.Single(results);
        Assert.Same(assembly.Calculator(), calculator.Node);
        Assert.Equal("Calculator<T>", calculator.Text);
        Assert.Equal(Trees.FixturesNamespace, calculator.Location);
        Assert.Equal("ILSight.Tests", calculator.Node.Assembly.Name);
    }

    [Fact]
    public async Task Members_report_their_declaring_type_and_skip_accessors()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var tryGet = await CollectAsync(list.SearchAsync("TRYGET", SearchKinds.Members));
        var area = await CollectAsync(list.SearchAsync("Area", SearchKinds.Members));

        var hit = Assert.Single(tryGet);
        Assert.Same(assembly.Calculator().Child("TryGet(int, out T?) : bool"), hit.Node);
        Assert.Equal(Trees.FixturesNamespace + ".Calculator<T>", hit.Location);
        Assert.Equal([Trees.FixturesNamespace + ".Circle", Trees.FixturesNamespace + ".IShape"], area.Select(r => r.Location).Order());
        Assert.All(area, r => Assert.Equal(MemberShape.Property, r.Node.MemberShape));
    }

    [Fact]
    public async Task Nested_types_and_constants_are_found()
    {
        var (list, _) = Trees.OpenTestAssembly();

        var results = await CollectAsync(list.SearchAsync("mode"));
        var precise = await CollectAsync(list.SearchAsync("precise"));

        Assert.Contains(results, r => r.Node.Is(TypeShape.Enum) && r.Location == Trees.FixturesNamespace + ".Calculator<T>");
        Assert.Contains(precise, r => r.Node.Is(MemberShape.Constant) && r.Location == Trees.FixturesNamespace + ".Mode");
    }

    [Fact]
    public async Task An_empty_query_finds_nothing()
    {
        var (list, _) = Trees.OpenTestAssembly();

        Assert.Empty(await CollectAsync(list.SearchAsync("  ")));
    }

    [Fact]
    public async Task CoreLib_searches_well_under_a_second()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var coreLib = list.Open(Trees.CoreLibPath);
        _ = coreLib.Root.Children;

        var stopwatch = Stopwatch.StartNew();
        var results = await CollectAsync(list.SearchAsync("Dictionary"));
        stopwatch.Stop();

        Assert.Contains(results, r => r.Text == "Dictionary<TKey, TValue>" && r.Location == "System.Collections.Generic");
        Assert.Contains(results, r => r.Node.Is(MemberShape.Method));
        Assert.True(stopwatch.ElapsedMilliseconds < 500, $"Search took {stopwatch.ElapsedMilliseconds} ms for {results.Count} results");
    }

    [Fact]
    public async Task Results_stream_in_batches_and_stop_on_cancellation()
    {
        var list = new AssemblyList();
        list.Open(Trees.CoreLibPath);
        using var cancellation = new CancellationTokenSource();
        var batches = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var batch in list.SearchAsync("e", SearchKinds.TypesAndMembers, cancellation.Token))
            {
                Assert.NotEmpty(batch);
                batches++;
                cancellation.Cancel();
            }
        });
        Assert.Equal(1, batches);
    }

    [Fact]
    public async Task A_mode_prefix_keeps_results_to_that_kind()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var types = await CollectAsync(list.SearchAsync("t:Count"));
        var properties = await CollectAsync(list.SearchAsync("p:Count"));
        var events = await CollectAsync(list.SearchAsync("Changed", SearchKinds.Events));

        Assert.Empty(types);
        Assert.Contains(properties, r => r.Node == assembly.Calculator().Child("Count : int"));
        Assert.All(properties, r => Assert.Equal(MemberShape.Property, r.Node.MemberShape));
        Assert.Equal([MemberShape.Event], events.Select(r => r.Node.MemberShape).Distinct());
    }

    [Fact]
    public async Task A_term_with_a_dot_matches_qualified_names()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var type = await CollectAsync(list.SearchAsync("Fixtures.Calculator", SearchKinds.Types));
        var method = await CollectAsync(list.SearchAsync("Calculator.TryGet"));

        Assert.Equal(["Calculator<T>", "Mode"], type.Select(r => r.Text).Order());
        Assert.Contains(type, r => r.Node == assembly.Calculator());
        Assert.Equal(Trees.FixturesNamespace + ".Calculator<T>", Assert.Single(method).Location);
    }

    [Fact]
    public async Task A_term_with_angle_brackets_matches_names_with_their_type_parameters()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        list.Open(Trees.CoreLibPath);

        var type = await CollectAsync(list.SearchAsync("t:=Calculator<T>"));
        var other = await CollectAsync(list.SearchAsync("t:=Calculator<U>"));
        var nested = await CollectAsync(list.SearchAsync("t:Calculator<T>.Mode"));
        var method = await CollectAsync(list.SearchAsync("md:=Empty<T>"));
        var pair = await CollectAsync(list.SearchAsync("t:\"=Dictionary<TKey, TValue>\""));

        Assert.Same(assembly.Calculator(), Assert.Single(type).Node);
        Assert.Empty(other);
        Assert.Equal("Mode", Assert.Single(nested).Text);
        Assert.Contains(method, r => r.Location == "System.Array");
        Assert.Contains(pair, r => r.Location == "System.Collections.Generic");
    }

    [Fact]
    public async Task Shorter_names_are_fitter()
    {
        var (list, _) = Trees.OpenTestAssembly();

        var results = await CollectAsync(list.SearchAsync("innamespace:Tests.Fixtures t:c"));

        var fittest = results.Where(r => r.Text is "Circle" or "Callback" or "Calculator<T>").OrderByDescending(r => r.Fitness).ToList();
        Assert.Equal(["Circle", "Callback", "Calculator<T>"], fittest.Select(r => r.Text));
        Assert.Equal(1f / "Calculator".Length, fittest[^1].Fitness);
    }

    [Fact]
    public async Task Operators_exclude_require_whole_names_and_match_letters_in_order()
    {
        var (list, _) = Trees.OpenTestAssembly();

        var excluded = await CollectAsync(list.SearchAsync("t:Calc -Mode"));
        var exact = await CollectAsync(list.SearchAsync("t:=Size"));
        var fuzzy = await CollectAsync(list.SearchAsync("t:~clcltr"));
        var regex = await CollectAsync(list.SearchAsync("t:/^I.*e$/"));

        Assert.Equal(["Calculator<T>"], excluded.Where(r => r.Location == Trees.FixturesNamespace).Select(r => r.Text));
        Assert.Equal(["Size"], exact.Where(r => r.Location == Trees.FixturesNamespace).Select(r => r.Text));
        Assert.Contains(fuzzy, r => r.Text == "Calculator<T>");
        Assert.Contains(regex, r => r.Text == "IShape");
    }

    [Fact]
    public async Task Literals_find_constants_and_the_methods_that_load_them()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var number = await CollectAsync(list.SearchAsync("c:42"));
        var text = await CollectAsync(list.SearchAsync("\"Total: \"", SearchKinds.Literals));

        var describe = assembly.Calculator().Child("Describe() : string");
        Assert.Contains(number, r => r.Node == assembly.Calculator().Child("Answer : int"));
        Assert.Contains(number, r => r.Node == describe);
        Assert.Contains(text, r => r.Node == describe);
    }

    [Fact]
    public async Task A_metadata_token_finds_its_definition()
    {
        var (list, assembly) = Trees.OpenTestAssembly();
        var token = typeof(Fixtures.Calculator<>).MetadataToken;

        var results = await CollectAsync(list.SearchAsync($"@{token:X8}"));

        Assert.Same(assembly.Calculator(), Assert.Single(results).Node);
    }

    [Fact]
    public async Task Namespaces_assemblies_and_resources_are_found_by_name()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var ns = await CollectAsync(list.SearchAsync("n:Tests.Fixtures"));
        var byName = await CollectAsync(list.SearchAsync("a:ilsight.tests"));
        var byPath = await CollectAsync(list.SearchAsync("af:.dll"));
        var resources = await CollectAsync(list.SearchAsync("r:logo"));

        Assert.Same(assembly.Fixtures(), Assert.Single(ns).Node);
        Assert.Same(assembly.Root, Assert.Single(byName).Node);
        Assert.Same(assembly.Root, Assert.Single(byPath).Node);
        Assert.Equal(["/Assets/logo.png", "logo.bmp", "logo.ico", "logo.png"], resources.Select(r => r.Text).Order());
    }

    [Fact]
    public async Task Only_namespaces_that_hold_types_are_found_not_those_that_only_enclose_others()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var found = await CollectAsync(list.SearchAsync("n:ILSight"));

        Assert.Equal(["ILSight.Tests", Trees.FixturesNamespace], found.Select(r => r.Text).Order());
        Assert.DoesNotContain(assembly.Root.Child("ILSight"), found.Select(r => r.Node));
    }

    [Fact]
    public async Task Scope_prefixes_keep_results_to_an_assembly_or_namespace()
    {
        var (list, _) = Trees.OpenTestAssembly();
        list.Open(Trees.CoreLibPath);

        var inTests = await CollectAsync(list.SearchAsync("inassembly:ILSight.Tests t:Size"));
        var inFixtures = await CollectAsync(list.SearchAsync("innamespace:Tests.Fixtures Area"));

        Assert.All(inTests, r => Assert.Equal("ILSight.Tests", r.Node.Assembly.Name));
        Assert.Contains(inTests, r => r.Text == "Size");
        Assert.Equal([Trees.FixturesNamespace + ".Circle", Trees.FixturesNamespace + ".IShape"], inFixtures.Select(r => r.Location).Order());
    }

    [Fact]
    public void The_query_syntax_parses_prefixes_quotes_and_scopes()
    {
        var query = SearchQueries.ParseQuery("md:\"Get Value\" inassembly:Core innamespace:\"\" -Async");

        Assert.Equal(SearchKinds.Methods, query.Kinds);
        Assert.Equal(["Get Value", "-Async"], Assert.IsType<SearchMatcher.Terms>(query.Matcher).Values);
        Assert.Equal("Core", query.InAssembly);
        Assert.Equal("", query.InNamespace);
        Assert.Equal(["System.Foo:Bar"], Assert.IsType<SearchMatcher.Terms>(SearchQueries.ParseQuery("System.Foo:Bar").Matcher).Values);
        Assert.False(SearchQueries.ParseQuery("inassembly:Core").CanFind);
    }

    [Fact]
    public void FromQuery_EachKindOfTerm_GivesItsLiteral()
    {
        Assert.Equal(255L, Assert.IsType<SearchLiteral.Integer>(LiteralOf("c:0xFF")).Value);
        Assert.Equal(-16L, Assert.IsType<SearchLiteral.Integer>(LiteralOf("c:-0x10")).Value);
        Assert.Equal(42L, Assert.IsType<SearchLiteral.Integer>(LiteralOf("c:42UL")).Value);
        Assert.Equal(-1L, Assert.IsType<SearchLiteral.Integer>(LiteralOf("c:18446744073709551615")).Value);
        Assert.Equal(1.5f, Assert.IsType<SearchLiteral.Single>(LiteralOf("c:1.5f")).Value);
        Assert.Equal(2.0, Assert.IsType<SearchLiteral.Double>(LiteralOf("c:2.0")).Value);
        Assert.Equal(1000.0, Assert.IsType<SearchLiteral.Double>(LiteralOf("c:1e3")).Value);
        Assert.Equal(2.5, Assert.IsType<SearchLiteral.Double>(LiteralOf("c:2.5d")).Value);
        Assert.IsType<SearchLiteral.Text>(LiteralOf("c:abc"));
        Assert.IsType<SearchLiteral.Text>(LiteralOf("c:0xZZ"));
        Assert.IsType<SearchLiteral.Text>(LiteralOf("c:1 2"));
        Assert.IsType<SearchLiteral.Text>(LiteralOf("c:/^4/"));
    }

    [Fact]
    public async Task SearchAsync_IntegerAndDoubleLiterals_FindOnlyLoadsOfTheirOwnKind()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var integer = await CollectAsync(list.SearchAsync("c:2"));
        var real = await CollectAsync(list.SearchAsync("c:2.0"));
        var pi = await CollectAsync(list.SearchAsync("c:3.141592653589793"));

        var describe = assembly.Calculator().Child("Describe() : string");
        var spread = assembly.Fixtures().Child("Circle", "Spread() : double");
        Assert.Contains(integer, r => r.Node == describe);
        Assert.DoesNotContain(integer, r => r.Node == spread);
        Assert.Contains(real, r => r.Node == spread);
        Assert.DoesNotContain(real, r => r.Node == describe);
        Assert.Contains(pi, r => r.Location == Trees.FixturesNamespace + ".Circle" && r.Node.MemberShape == MemberShape.Property);
    }

    [Fact]
    public async Task SearchAsync_SingleLiteral_FindsFloatConstantsOnly()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        list.Open(Trees.CoreLibPath);

        var single = await CollectAsync(list.SearchAsync("c:3.4028235E+38f"));
        var real = await CollectAsync(list.SearchAsync("c:3.4028235E+38"));

        Assert.Contains(single, r => r.Text == "MaxValue : float" && r.Location == "System.Single");
        Assert.DoesNotContain(real, r => r.Text == "MaxValue : float");
    }

    [Fact]
    public async Task ParseQuery_InvalidRegex_RecordsWhyAndFindsNothing()
    {
        var (list, _) = Trees.OpenTestAssembly();

        var query = SearchQueries.ParseQuery("t:/[a/");
        var withTerm = SearchQueries.ParseQuery("/(/ Calc");
        var results = await CollectAsync(list.SearchAsync("t:/[a/"));

        var invalid = Assert.IsType<SearchMatcher.InvalidRegex>(query.Matcher);
        Assert.NotEmpty(invalid.Message);
        Assert.False(query.CanFind);
        Assert.IsType<SearchMatcher.InvalidRegex>(withTerm.Matcher);
        Assert.Empty(results);
        Assert.IsType<SearchMatcher.Regex>(SearchQueries.ParseQuery("t:/^I.*e$/").Matcher);
    }

    private static SearchLiteral LiteralOf(string text)
    {
        return SearchLiteral.FromQuery(SearchQueries.ParseQuery(text));
    }

    [Fact]
    public async Task Kinds_combine_and_prefixes_add_up_in_place_of_the_kinds_picked()
    {
        var (list, assembly) = Trees.OpenTestAssembly();

        var picked = await CollectAsync(list.SearchAsync("Answer", SearchKinds.Fields | SearchKinds.Literals));
        var prefixed = SearchQueries.ParseQuery("t:Size p:Size", SearchKinds.All);

        Assert.Contains(picked, r => r.Node == assembly.Calculator().Child("Answer : int"));
        Assert.Equal(SearchKinds.Types | SearchKinds.Properties, prefixed.Kinds);
        Assert.Equal(SearchKinds.Assemblies, SearchQueries.ParseQuery("af:dll", SearchKinds.Types).Kinds);
        Assert.Equal(SearchKinds.Tokens, SearchQueries.ParseQuery("@06000001").Kinds);
    }

    [Fact]
    public void Scoping_replaces_an_existing_scope_and_goes_first()
    {
        Assert.Equal("inassembly:Other Foo ", SearchQueries.AddScope("inassembly:Core Foo", "inassembly", "Other"));
        Assert.Equal("innamespace:\"\" ", SearchQueries.AddScope("", "innamespace", ""));
    }

    private static async Task<List<SearchResult>> CollectAsync(IAsyncEnumerable<IReadOnlyList<SearchResult>> search)
    {
        var results = new List<SearchResult>();
        await foreach (var batch in search)
        {
            results.AddRange(batch);
        }

        return results;
    }
}

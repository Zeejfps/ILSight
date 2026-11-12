using ILSight.Core;
using ILSight.Core.Comparison;

namespace ILSight.Tests;

public sealed class ApiDiffTests
{
    private const string SampleNamespace = "ApiDiffSample";

    private static readonly Lazy<(AssemblyList List, LoadedModule Left, LoadedModule Right)> s_opened = new(OpenBoth);
    private static readonly SharedRead<ApiDiff> s_public = new(() => ReadDiffAsync(ApiScope.PublicApi));
    private static readonly SharedRead<ApiDiff> s_all = new(() => ReadDiffAsync(ApiScope.AllDeclared));

    private static (AssemblyList List, LoadedModule Left, LoadedModule Right) OpenBoth()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var left = list.OpenModule(Trees.ApiDiffOlderPath);
        var right = list.OpenModule(Trees.ApiDiffNewerPath);
        return (list, left, right);
    }

    private static async Task<ApiDiff> ReadDiffAsync(ApiScope scope)
    {
        var opened = s_opened.Value;
        var compared = await ApiEntries.CompareAsync(opened.Left, opened.Right, scope, CancellationToken.None);
        return Assert.IsType<TypeSystemWork<ApiDiff>.Done>(compared).Value;
    }

    private static ApiEntry Entry(ApiDiff diff, params ReadOnlySpan<string> path)
    {
        var entries = diff.Namespaces;
        ApiEntry? entry = null;
        foreach (var text in path)
        {
            entry = Named(entries, text);
            entries = entry.Children;
        }
        Assert.NotNull(entry);
        return entry;
    }

    private static ApiEntry Named(IReadOnlyList<ApiEntry> entries, string text)
    {
        var found = entries.FirstOrDefault(e => e.Text == text);
        if (found is null)
        {
            throw new InvalidOperationException($"No entry '{text}' among: {string.Join(", ", entries.Select(e => e.Text))}");
        }

        return found;
    }

    private static ApiSides.Both Both(ApiEntry entry)
    {
        return Assert.IsType<ApiSides.Both>(Assert.IsType<ApiEntry.Entity>(entry).Sides);
    }

    private static async Task<ApiEntry> PublicAsync(params string[] path)
    {
        return Entry(await s_public.ReadAsync(), path);
    }

    private static async Task<ApiEntry> AllAsync(params string[] path)
    {
        return Entry(await s_all.ReadAsync(), path);
    }

    [Fact]
    public async Task The_comparison_names_both_assemblies_with_their_versions()
    {
        var diff = await s_public.ReadAsync();

        Assert.Equal("ApiDiffSample (1.0.0.0)", diff.LeftName);
        Assert.Equal("ApiDiffSample (2.0.0.0)", diff.RightName);
    }

    [Fact]
    public async Task What_is_declared_alike_on_both_sides_is_unchanged_and_points_at_both()
    {
        var deposit = await PublicAsync(SampleNamespace, "Account", "Deposit(decimal) : void");
        var statement = await PublicAsync(SampleNamespace, "Account", "Statement");

        Assert.Equal(ApiChange.Unchanged, deposit.Change);
        Assert.False(deposit.HasChanges);
        Assert.False(statement.HasChanges);
        Assert.Equal(Both(deposit).Left.Declaration, Both(deposit).Right.Declaration);
    }

    [Fact]
    public async Task A_member_on_one_side_only_is_added_or_removed_and_points_at_that_side()
    {
        var transfer = await PublicAsync(SampleNamespace, "Account", "Transfer(Account, decimal) : void");
        var withdraw = await PublicAsync(SampleNamespace, "Account", "Withdraw(decimal) : void");

        Assert.Equal(ApiChange.Added, transfer.Change);
        Assert.IsType<ApiSides.RightOnly>(Assert.IsType<ApiEntry.Entity>(transfer).Sides);
        Assert.Equal(ApiChange.Removed, withdraw.Change);
        Assert.IsType<ApiSides.LeftOnly>(Assert.IsType<ApiEntry.Entity>(withdraw).Sides);
    }

    [Fact]
    public async Task Each_overload_is_matched_alone()
    {
        var ledger = await PublicAsync(SampleNamespace, "Ledger");

        Assert.Equal(ApiChange.Unchanged, Named(ledger.Children, "Add(int) : void").Change);
        Assert.Equal(ApiChange.Removed, Named(ledger.Children, "Add(string) : void").Change);
        Assert.Equal(ApiChange.Added, Named(ledger.Children, "Add(long) : void").Change);
    }

    [Fact]
    public async Task A_member_declared_differently_is_changed_with_both_declarations()
    {
        var describe = await PublicAsync(SampleNamespace, "Account", "Describe() : string");
        var balance = await PublicAsync(SampleNamespace, "Account", "Balance : decimal");
        var maxOwners = await PublicAsync(SampleNamespace, "Account", "MaxOwners : int");

        Assert.Equal(ApiChange.Changed, describe.Change);
        Assert.Equal("public virtual string Describe()", Both(describe).Left.Declaration);
        Assert.Equal("public string Describe()", Both(describe).Right.Declaration);
        Assert.Equal(ApiChange.Changed, balance.Change);
        Assert.Equal("public decimal Balance { get; private set; }", Both(balance).Left.Declaration);
        Assert.Equal("public decimal Balance { get; set; }", Both(balance).Right.Declaration);
        Assert.Equal(ApiChange.Changed, maxOwners.Change);
        Assert.Equal("public const int MaxOwners = 2", Both(maxOwners).Left.Declaration);
        Assert.Equal("public const int MaxOwners = 3", Both(maxOwners).Right.Declaration);
    }

    [Fact]
    public async Task A_type_made_obsolete_is_changed()
    {
        var legacy = await PublicAsync(SampleNamespace, "Legacy");

        Assert.Equal(ApiChange.Changed, legacy.Change);
        Assert.Equal("[Obsolete(\"Use Account.Describe\")] public static class Legacy", Both(legacy).Right.Declaration);
        Assert.Equal(ApiChange.Unchanged, Named(legacy.Children, "Run() : void").Change);
    }

    [Fact]
    public async Task A_parameter_that_turns_nullable_changes_its_member_rather_than_replacing_it()
    {
        var note = await PublicAsync(SampleNamespace, "Notes", "Note(string?) : void");

        Assert.Equal(ApiChange.Changed, note.Change);
        Assert.Equal("public void Note(string)", Both(note).Left.Declaration);
        Assert.Equal("public void Note(string?)", Both(note).Right.Declaration);
    }

    [Fact]
    public async Task A_type_that_only_gained_a_member_is_unchanged_itself_but_has_changes()
    {
        var currency = await PublicAsync(SampleNamespace, "Currency");

        Assert.Equal(ApiChange.Unchanged, currency.Change);
        Assert.True(currency.HasChanges);
        Assert.Equal(ApiChange.Added, Named(currency.Children, "Pound : Currency").Change);
        Assert.DoesNotContain(currency.Children, static c => c.Text.StartsWith("value__"));
    }

    [Fact]
    public async Task A_namespace_on_one_side_only_is_added_or_removed_with_its_types()
    {
        var reports = await PublicAsync("ApiDiffSample.Reports");
        var old = await PublicAsync("ApiDiffSample.Old");

        Assert.Equal(ApiChange.Added, reports.Change);
        Assert.Equal(ApiChange.Added, Named(reports.Children, "Summary").Change);
        Assert.Equal(ApiChange.Removed, old.Change);
        Assert.Equal(ApiChange.Removed, Named(old.Children, "Retired").Change);
        Assert.Equal(ApiChange.Removed, Entry(await s_public.ReadAsync(), "ApiDiffSample.Old", "Retired", "Close() : void").Change);
    }

    [Fact]
    public async Task The_public_api_leaves_out_what_other_assemblies_cannot_use()
    {
        var account = await PublicAsync(SampleNamespace, "Account");

        Assert.Equal(ApiChange.Removed, (await PublicAsync(SampleNamespace, "Helper")).Change);
        Assert.DoesNotContain(account.Children, static c => c.Text.StartsWith("Audit"));
    }

    [Fact]
    public async Task All_that_is_declared_takes_in_what_is_not_public_and_never_what_the_compiler_made_up()
    {
        var account = await AllAsync(SampleNamespace, "Account");

        Assert.Equal(ApiChange.Changed, (await AllAsync(SampleNamespace, "Helper")).Change);
        Assert.Equal(ApiChange.Added, Named(account.Children, "AuditCount : int").Change);
        Assert.Equal(ApiChange.Unchanged, Named(account.Children, "Audit() : void").Change);
        Assert.DoesNotContain(account.Children, static c => c.Text.Contains("k__BackingField"));
        Assert.DoesNotContain((await s_all.ReadAsync()).Namespaces.SelectMany(static n => n.Children), static c => c.Text == "<Module>");
    }

    [Fact]
    public async Task Entries_sort_as_the_tree_sorts_them_types_then_fields_properties_and_methods_by_name()
    {
        var account = await PublicAsync(SampleNamespace, "Account");

        var texts = account.Children
            .Select(static c => c.Text)
            .ToList();

        Assert.Equal("Statement", texts[0]);
        Assert.Equal("MaxOwners : int", texts[1]);
        Assert.True(texts.IndexOf("Owner : string") < texts.IndexOf("Deposit(decimal) : void"));
        Assert.True(texts.IndexOf("Deposit(decimal) : void") < texts.IndexOf("Withdraw(decimal) : void"));
    }

    [Fact]
    public async Task The_change_of_a_type_or_member_follows_from_its_sides_in_every_case()
    {
        var describe = Both(await PublicAsync(SampleNamespace, "Account", "Describe() : string"));

        Assert.Equal(ApiChange.Removed, new ApiSides.LeftOnly(describe.Left).Change);
        Assert.Equal(ApiChange.Added, new ApiSides.RightOnly(describe.Right).Change);
        Assert.Equal(ApiChange.Changed, new ApiSides.Both { Left = describe.Left, Right = describe.Right }.Change);
        Assert.Equal(ApiChange.Unchanged, new ApiSides.Both { Left = describe.Right, Right = describe.Right }.Change);
        Assert.Equal(ApiChange.Added, Entity(new ApiSides.RightOnly(describe.Right)).Change);
    }

    [Theory]
    [InlineData(ApiPresence.LeftOnly, ApiChange.Removed)]
    [InlineData(ApiPresence.RightOnly, ApiChange.Added)]
    [InlineData(ApiPresence.Both, ApiChange.Unchanged)]
    public void The_change_of_a_namespace_follows_from_where_it_is(ApiPresence presence, ApiChange expected)
    {
        Assert.Equal(expected, Namespace(presence, []).Change);
    }

    [Fact]
    public async Task An_unchanged_entry_has_changes_only_when_something_it_holds_does()
    {
        var deposit = Both(await PublicAsync(SampleNamespace, "Account", "Deposit(decimal) : void"));
        var unchanged = Entity(deposit);
        var added = Entity(new ApiSides.RightOnly(deposit.Right));

        Assert.False(Namespace(ApiPresence.Both, [unchanged]).HasChanges);
        Assert.True(Namespace(ApiPresence.Both, [unchanged, added]).HasChanges);
        Assert.True(Namespace(ApiPresence.Both, [Namespace(ApiPresence.Both, [added])]).HasChanges);
        Assert.True(Namespace(ApiPresence.LeftOnly, []).HasChanges);
    }

    private static ApiEntry.Entity Entity(ApiSides sides)
    {
        return new ApiEntry.Entity
        {
            Shape = new SymbolShape.Member(MemberShape.Method),
            Text = "Deposit(decimal) : void",
            Accessibility = NodeAccessibility.Public,
            Children = [],
            Sides = sides,
        };
    }

    private static ApiEntry.Namespace Namespace(ApiPresence presence, IReadOnlyList<ApiEntry> children)
    {
        return new ApiEntry.Namespace
        {
            Shape = new SymbolShape.Namespace(),
            Text = SampleNamespace,
            Accessibility = NodeAccessibility.None,
            Children = children,
            Presence = presence,
        };
    }

    [Fact]
    public async Task An_added_or_removed_type_counts_once_without_its_members()
    {
        var diff = await s_public.ReadAsync();

        Assert.Equal(4, diff.Added);
        Assert.Equal(4, diff.Removed);
        Assert.Equal(5, diff.Changed);
    }

    [Fact]
    public async Task An_entry_points_at_its_definition_in_each_assembly()
    {
        var opened = s_opened.Value;
        var deposit = await PublicAsync(SampleNamespace, "Account", "Deposit(decimal) : void");

        var left = await opened.List.FindNodeAsync(Both(deposit).Left.Reference);
        var right = await opened.List.FindNodeAsync(Both(deposit).Right.Reference);

        Assert.Same(opened.Left, left?.Assembly);
        Assert.Same(opened.Right, right?.Assembly);
    }

    [Fact]
    public async Task A_cancelled_comparison_stops()
    {
        var opened = s_opened.Value;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ApiEntries.CompareAsync(opened.Left, opened.Right, ApiScope.PublicApi, cancelled.Token));
    }
}

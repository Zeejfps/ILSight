using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Testing;
using ILSight.Core;
using ILSight.Core.Analysis;
using ZGF.KeyboardModule;
using ILSight.CodeViewArea;
using ILSight.CommandArea;
using static ILSight.Analysis;
using static ILSight.App.Tests.BuiltInGlyphs;

namespace ILSight.App.Tests;

public sealed class AnalyzerTests
{
    private static async Task<BrowseNode> FirstAsync(AppStateDriver d)
    {
        return (await d.FixtureAsync("Outline").ListChildrenAsync()).Single(c => c.Text.StartsWith("First()"));
    }

    private static AnalyzerKindNode Expand(AppStateDriver d, AnalyzerSymbolNode symbol, string header)
    {
        var rows = d.Ui.Analyzer;
        var kind = rows.ListChildren(symbol).OfType<AnalyzerKindNode>().Single(k => Core.Analysis.Analyzers.GetHeader(k.Kind) == header);
        rows.View.SetExpanded(kind, true);
        rows.GetChildren(kind);
        d.RunUntil(() => kind.IsDone);
        return kind;
    }

    private static AnalysisSymbol SymbolOf(BrowseNode node)
    {
        return node.Analysis ?? throw new InvalidOperationException($"{node.Text} stands for nothing to analyze.");
    }

    private static List<string> ListedPaths(AppStateDriver d)
    {
        return d.AppState.Assemblies.Listed.Select(static a => a.FilePath.Text).ToList();
    }

    [Fact]
    public async Task Analyzing_a_member_docks_the_analyzer_and_its_analyses_find_what_uses_it()
    {
        var d = AppStateDriver.Opened();

        d.Analyze(await FirstAsync(d));
        var root = Assert.Single(d.AppState.Analyzer.Found());
        var usedBy = Expand(d, root, "Used By");

        Assert.True(d.Ui.Layout.Contains(DockPanels.Analyzer));
        Assert.StartsWith("First()", root.Symbol.Label);
        var caller = Assert.IsType<AnalyzerSymbolNode>(Assert.Single(usedBy.Found));
        Assert.StartsWith("Third()", caller.Symbol.Label);
    }

    [Fact]
    public async Task A_failed_analysis_tells_why_in_its_row_and_reports_the_whole_error()
    {
        var d = AppStateDriver.Opened();
        var elsewhere = await new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath).ReadRootAsync().GetChildAsync(AppStateDriver.FixturesNamespace, "Outline");
        var symbol = elsewhere.Analysis;
        Assert.NotNull(symbol);
        var kind = new AnalyzerKindNode { Subject = symbol, Kind = symbol.Kinds.Single(k => Core.Analysis.Analyzers.GetHeader(k) == "Used By") };

        d.InTurn(() => Commands.StartAnalysis(d.AppState, StartRun(kind)));
        d.RunUntil(() => kind.IsDone);

        var message = Assert.IsType<AnalyzerMessageNode>(Assert.Single(kind.Found));
        Assert.NotNull(message.Error);
        var error = Assert.Single(d.AppState.Errors.Entries);
        Assert.Equal($"Cannot analyze {symbol.Label} (Used By)", error.Words().Title);
        Assert.Contains(nameof(InvalidOperationException), error.Words().Details);
        Assert.Contains(" at ", error.Words().Details);
    }

    [Theory]
    [InlineData("Count", "Increment()")]
    [InlineData("Changed", "Watch(")]
    public async Task Used_By_a_property_or_event_finds_each_member_using_its_accessors_once(string member, string user)
    {
        var d = AppStateDriver.Opened();
        var node = (await d.FixtureAsync("Counter").ListChildrenAsync())
            .Where(c => !c.IsCompilerGenerated)
            .Single(c => c.Text.StartsWith(member + " "));

        d.Analyze(node);
        var usedBy = Expand(d, Assert.Single(d.AppState.Analyzer.Found()), "Used By");

        var found = Assert.IsType<AnalyzerSymbolNode>(Assert.Single(usedBy.Found));
        Assert.StartsWith(user, found.Symbol.Label);
    }

    [Fact]
    public async Task Applied_To_finds_members_that_carry_a_security_attribute()
    {
        var d = AppStateDriver.Opened();

        d.Analyze(await d.FixtureAsync("GuardAttribute"));
        var appliedTo = Expand(d, Assert.Single(d.AppState.Analyzer.Found()), "Applied To");

        var member = Assert.IsType<AnalyzerSymbolNode>(Assert.Single(appliedTo.Found));
        Assert.StartsWith("Enter()", member.Symbol.Label);
    }

    [Fact]
    public async Task A_result_opens_its_member_when_activated_and_can_be_analyzed_in_turn()
    {
        var app = AppDriver.Opened(height: 600f);
        var d = app.Driver;
        d.Analyze(await FirstAsync(d));
        var analyzer = d.AppState.Analyzer;
        var caller = (AnalyzerSymbolNode)Expand(d, analyzer.Found()[0], "Used By").Found[0];
        app.Ui.Analyzer.View.Select(caller);
        app.Run(3);

        app.DoubleClickOn(Selector.WithText(app.Frames.Canvas.Texts.Last(t => t.Inputs.Text.StartsWith("Third()")).Inputs.Text));
        d.Settle();
        d.InTurn(() => Commands.Analyze(app.AppState, app.Ui, caller.Symbol));

        Assert.StartsWith("Third()", d.AppState.Navigation.ActiveTab?.Node?.Text);
        Assert.Equal(2, analyzer.Roots.Count);
        Assert.Same(analyzer.Roots[1], app.Ui.Analyzer.View.Selected);
    }

    [Fact]
    public async Task Ctrl_R_analyzes_the_node_selected_in_the_focused_tree()
    {
        var app = AppDriver.Opened();
        var d = app.Driver;
        d.Ui.Tree.RevealNode(await FirstAsync(d));
        app.Run(2);
        app.Frames.ClickOn(GetFixtureRow("Outline", (await FirstAsync(d)).Text));
        d.Settle();

        app.Frames.Key(Shortcuts.Analyze).Frame(2);

        Assert.StartsWith("First()", Assert.Single(d.AppState.Analyzer.Found()).Symbol.Label);
    }

    [Fact]
    public async Task Ctrl_R_in_the_code_analyzes_the_reference_at_the_caret()
    {
        var view = CodeViewDriver.Showing(await CodeViewDriver.OutlineAsync(), 1200f, frames: 2);
        var (line, column) = view.Document.GetPosition("return First();");

        view.Click(line, column + "return First".Length);
        view.Key(Shortcuts.Analyze);

        Assert.Equal("First", Assert.IsType<CodeViewIntent.Analyze>(view.Last.Intent).Entity.Name);
    }

    [Fact]
    public async Task Ctrl_R_in_the_analyzer_analyzes_the_result_it_has_selected()
    {
        var app = AppDriver.Opened(height: 600f);
        var d = app.Driver;
        await ShowFirstCallerAsync(app);
        app.Frames.ClickOn(GetAnalyzerRow("First() : int", "Used By", "Third() : int")).Frame();

        app.Frames.Key(Shortcuts.Analyze).Frame(2);

        Assert.Equal(2, d.AppState.Analyzer.Roots.Count);
        Assert.StartsWith("Third()", d.AppState.Analyzer.Found()[1].Symbol.Label);
    }

    [Fact]
    public async Task Ctrl_R_on_an_analyzer_root_does_what_its_menu_offers_there_nothing()
    {
        var app = AppDriver.Opened(height: 600f);
        var d = app.Driver;
        await ShowFirstCallerAsync(app);
        var root = Assert.Single(d.AppState.Analyzer.Roots);
        app.Frames.ClickOn(GetAnalyzerRow("First() : int")).Frame();
        app.Ui.Analyzer.View.SetExpanded(root, false);
        app.Run(1);

        app.Frames.Key(Shortcuts.Analyze).Frame(2);
        app.Frames.ClickOn(GetAnalyzerRow("First() : int"), PointerButton.Right).Frame();

        Assert.False(app.Ui.Analyzer.View.IsExpanded(root));
        Assert.Same(root, Assert.Single(d.AppState.Analyzer.Roots));
        Assert.Empty(d.AppState.Errors.Entries);
        Assert.True(app.Frames.Drew("Clear"));
        Assert.False(app.Frames.Drew(app.Ui.Strings.TreeAnalyze));
    }

    [Fact]
    public async Task Analyze_on_a_symbol_whose_assembly_cannot_be_listed_is_unavailable_saying_why_as_adding_it_refuses()
    {
        var d = AppStateDriver.Opened();
        var analyzer = d.AppState.Analyzer;
        var greeter = (await d.FixtureAsync("Greeter")).Analysis;
        Assert.NotNull(greeter);
        var unlisted = new AnalysisSymbol
        {
            Shape = greeter.Shape,
            Accessibility = greeter.Accessibility,
            Label = greeter.Label,
            Location = greeter.Location,
            Reference = null,
            Kinds = greeter.Kinds,
            Accessors = greeter.Accessors,
            File = AssemblyPath.Of(Path.Combine(Path.GetTempPath(), "Elsewhere", "Unlisted.dll")),
            Definition = null,
        };

        var offered = Analysis.OfferAnalysis(analyzer, greeter);
        var unavailable = Analysis.OfferAnalysis(analyzer, unlisted);

        Assert.Equal(greeter, Assert.IsType<CommandOffer<AnalysisSymbol>.Runs>(offered).Target);
        var why = Assert.IsType<AppProblem.NotAnalyzable>(Assert.IsType<CommandOffer<AnalysisSymbol>.Unavailable>(unavailable).Why);
        Assert.Equal(why, Assert.IsType<AnalyzerAdd.Refused>(analyzer.AddRoot(unlisted)).Problem);
        Assert.Empty(analyzer.Roots);
    }

    [Fact]
    public async Task Analyzing_a_result_in_an_assembly_loaded_only_to_resolve_references_lists_it_as_following_it_does_and_analyzes_it_there()
    {
        var d = AppStateDriver.Opened();
        var analyzer = d.AppState.Analyzer;
        d.Analyze((await d.FixtureAsync("Outline").ListChildrenAsync()).Single(c => c.Text.StartsWith("Second()")));
        var uses = Expand(d, analyzer.Found()[0], "Uses");
        var append = uses.Found.OfType<AnalyzerSymbolNode>().First(n => n.Symbol.Label.StartsWith("Append("));
        var file = append.Symbol.File;
        Assert.NotNull(file);
        var reference = append.Symbol.Reference;
        Assert.NotNull(reference);
        var listedBefore = ListedPaths(d);

        d.InTurn(() => Commands.Analyze(d.AppState, d.Ui, append.Symbol));
        var waiting = analyzer.Roots.ToList();
        d.Settle();
        var followed = AppStateDriver.Opened();
        followed.Start((a, ct) => a.NavigateHandler.HandleAsync(a, reference, a.Navigation.BeginResolving(inNewTab: false), ct));
        followed.Settle();

        Assert.Equal([AppStateDriver.FixturePath], listedBefore);
        Assert.IsType<AnalyzerPendingNode>(waiting[1]);
        Assert.Equal([AppStateDriver.FixturePath, file.Text], ListedPaths(d));
        Assert.Equal(ListedPaths(followed), ListedPaths(d));
        var root = analyzer.Found()[1];
        Assert.Same(append.Symbol, root.Symbol);
        Assert.True(root.Symbol.IsIn(d.AppState.Assemblies.Listed[1]));
        Assert.Same(root, d.Ui.Analyzer.View.Selected);
        Assert.Empty(d.AppState.Errors.Entries);
    }

    [Fact]
    public void Analyzing_a_symbol_whose_assembly_nothing_can_list_adds_nothing_and_says_why()
    {
        var d = AppStateDriver.Opened();
        var nowhere = new AnalysisSymbol
        {
            Shape = new SymbolShape.Assembly(), Accessibility = NodeAccessibility.Public, Label = "Nowhere", Location = "",
            Reference = null, Kinds = [], Accessors = [], File = null, Definition = null,
        };

        d.InTurn(() => Commands.Analyze(d.AppState, d.Ui, nowhere));
        d.Settle();

        Assert.Empty(d.AppState.Analyzer.Roots);
        var error = Assert.Single(d.AppState.Errors.Entries);
        Assert.Equal("Cannot analyze Nowhere", error.Words().Title);
        Assert.Equal("Its assembly isn't open, and nothing names its definition to open it by.", error.Words().Details);
    }

    private static async Task ShowFirstCallerAsync(AppDriver app)
    {
        var d = app.Driver;
        var analyzer = d.AppState.Analyzer;
        d.Analyze(await FirstAsync(d));
        var caller = Expand(d, analyzer.Found()[0], "Used By").Found[0];
        d.Ui.Analyzer.View.ScrollIntoView(caller);
        app.Run(3);
    }


    [Fact]
    public async Task A_reference_in_the_code_is_analyzed_once_it_is_found()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(await d.FixtureAsync("Outline"));
        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var document = tab.Document;
        var (line, column) = document.GetPosition("return First();");
        var reference = Assert.IsAssignableFrom<CodeReference.Navigable>(document.SpanAt(line, column + "return ".Length).Reference);
        d.TakeViewCommands();

        d.InTurn(() => Commands.StartAnalyzing(d.AppState, BeginAnalyzing(d.AppState.Analyzer, reference)));
        d.Settle();

        var root = Assert.Single(d.AppState.Analyzer.Found());
        Assert.StartsWith("First()", root.Symbol.Label);
        Assert.Equal([new ViewCommand.ShowAnalysis(root)], d.TakeViewCommands());
    }

    private static async Task<CodeReference.Entity> ReferenceToAsync(AppStateDriver d, string type, string text, int offset = 0)
    {
        d.Show(await d.FixtureAsync(type));
        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var document = tab.Document;
        var (line, column) = document.GetPosition(text);
        return Assert.IsType<CodeReference.Entity>(document.SpanAt(line, column + offset).Reference);
    }

    private static async Task<CodeReference.Entity> ReferenceToFirstAsync(AppStateDriver d)
    {
        return await ReferenceToAsync(d, "Outline", "return First();", "return ".Length);
    }

    [Fact]
    public async Task A_reference_shows_a_loading_root_at_once_that_is_saved_once_it_is_found()
    {
        var d = AppStateDriver.Opened();
        var reference = await ReferenceToFirstAsync(d);
        var analyzer = d.AppState.Analyzer;

        d.Analyze(reference);
        var waiting = analyzer.Roots.ToList();
        var savedWhileFinding = analyzer.ListSavedAnalyses();
        var revisionWhileFinding = analyzer.Revision;
        d.Settle();

        Assert.IsType<AnalyzerPendingNode>(Assert.Single(waiting));
        Assert.Empty(savedWhileFinding);
        Assert.StartsWith("First()", Assert.Single(analyzer.Found()).Symbol.Label);
        Assert.Single(analyzer.ListSavedAnalyses());
        Assert.NotEqual(revisionWhileFinding, analyzer.Revision);
    }

    [Fact]
    public async Task Two_analyses_asked_for_by_reference_both_add_their_roots_in_the_order_asked()
    {
        var d = AppStateDriver.Opened();
        var length = await ReferenceToAsync(d, "Greeter", "Length");
        var greet = await ReferenceToAsync(d, "Greeter", "Greet(2)");

        d.Analyze(length);
        d.Analyze(greet);
        d.Settle();

        var labels = d.AppState.Analyzer.Found().Select(r => r.Symbol.Label).ToList();
        Assert.Equal(2, labels.Count);
        Assert.StartsWith("Length", labels[0]);
        Assert.StartsWith("Greet(", labels[1]);
    }

    [Fact]
    public async Task A_reference_to_a_symbol_analyzed_already_selects_its_root()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var reference = await ReferenceToFirstAsync(d);
        var analyzer = d.AppState.Analyzer;
        var first = Assert.IsType<AnalyzerAdd.Analyzing>(analyzer.AddRoot(SymbolOf(await FirstAsync(d)))).Root;
        var greeter = Assert.IsType<AnalyzerAdd.Analyzing>(analyzer.AddRoot(SymbolOf(await d.FixtureAsync("Greeter")))).Root;
        d.TakeViewCommands();

        d.InTurn(() => Commands.StartAnalyzing(d.AppState, BeginAnalyzing(analyzer, reference)));
        d.Settle();

        Assert.Equal<AnalyzerNode>([first, greeter], analyzer.Roots);
        Assert.Equal([new ViewCommand.ShowAnalysis(first)], d.TakeViewCommands());
    }

    [Fact]
    public async Task A_reference_whose_root_was_removed_meanwhile_is_dropped()
    {
        var d = AppStateDriver.Opened();
        var reference = await ReferenceToFirstAsync(d);
        var analyzer = d.AppState.Analyzer;

        d.Analyze(reference);
        analyzer.RemoveRoot(analyzer.Roots[0]);
        d.Settle();

        Assert.Empty(analyzer.Roots);
    }

    [Fact]
    public async Task A_reference_that_does_not_resolve_removes_its_root_and_is_reported()
    {
        using var temp = new TempDir();
        var copy = temp.Write("Copy.dll", await File.ReadAllBytesAsync(AppStateDriver.FixturePath));
        var d = new AppStateDriver();
        d.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(copy)], ct));
        d.RunUntil(() => d.AppState.Assemblies.Assemblies.Count == 1);
        d.Settle();
        var assembly = d.AppState.Assemblies.Assemblies.Single();
        var outline = await assembly.ReadRootAsync().GetChildAsync(AppStateDriver.FixturesNamespace, "Outline");
        var document = await outline.DecompileAsync(DecompilationLanguage.CSharp);
        var (line, column) = document.GetPosition("return First();");
        var reference = Assert.IsAssignableFrom<CodeReference.Navigable>(document.SpanAt(line, column + "return ".Length).Reference);
        d.InTurn(() => Commands.CloseAssemblies(d.AppState, [assembly]));
        File.Delete(copy);

        d.Analyze(reference);
        d.Settle();

        Assert.Empty(d.AppState.Analyzer.Roots);
        Assert.Equal("Cannot resolve First", Assert.Single(d.AppState.Errors.Entries).Words().Title);
    }

    [Fact]
    public async Task Removing_a_root_or_clearing_the_assembly_list_empties_the_analyzer()
    {
        var d = AppStateDriver.Opened();
        var analyzer = d.AppState.Analyzer;
        d.Analyze(await FirstAsync(d));
        d.Analyze(await d.FixtureAsync("Greeter"));

        analyzer.RemoveRoot(analyzer.Roots[0]);
        var left = analyzer.Found().Select(r => r.Symbol.Label).ToList();
        d.InTurn(() => Commands.CloseAllAssemblies(d.AppState));

        Assert.Equal(["Greeter"], left);
        Assert.Empty(analyzer.Roots);
    }

    [Fact]
    public async Task Removing_a_root_leaves_the_analyses_of_results_never_expanded_unlisted()
    {
        var d = AppStateDriver.Opened();
        var analyzer = d.AppState.Analyzer;
        d.Analyze(await FirstAsync(d));
        var root = analyzer.Found()[0];
        var caller = (AnalyzerSymbolNode)Expand(d, root, "Used By").Found[0];

        analyzer.RemoveRoot(root);

        Assert.Null(caller.Children);
    }

    private static async Task<AnalyzerKindNode> NewKindAsync(AppStateDriver d)
    {
        var symbol = (await FirstAsync(d)).Analysis;
        Assert.NotNull(symbol);
        return new AnalyzerKindNode { Subject = symbol, Kind = symbol.Kinds[0] };
    }

    private static async Task<AnalyzerKindNode> RunningKindAsync(AppStateDriver d)
    {
        var kind = await NewKindAsync(d);
        kind.Run = new AnalyzerRun.Running(new CancellationTokenSource());
        return kind;
    }

    [Fact]
    public async Task Cancelling_a_symbol_cancels_the_analyses_listed_under_it()
    {
        var d = AppStateDriver.Opened();
        var running = await RunningKindAsync(d);
        var work = running.Work;
        Assert.NotNull(work);
        var symbol = new AnalyzerSymbolNode { Symbol = running.Subject, Children = [running] };

        CancelNode(symbol);

        Assert.True(work.IsCancellationRequested);
        Assert.Null(running.Work);
    }

    [Fact]
    public async Task Cancelling_a_symbol_whose_children_were_never_listed_leaves_them_unlisted()
    {
        var d = AppStateDriver.Opened();
        var analyzed = (await FirstAsync(d)).Analysis;
        Assert.NotNull(analyzed);
        var symbol = new AnalyzerSymbolNode { Symbol = analyzed };

        CancelNode(symbol);

        Assert.Null(symbol.Children);
    }

    [Fact]
    public async Task Cancelling_an_analysis_cancels_its_run_and_the_analyses_under_what_it_found()
    {
        var d = AppStateDriver.Opened();
        var kind = await RunningKindAsync(d);
        var nested = await RunningKindAsync(d);
        kind.Found.Add(new AnalyzerSymbolNode { Symbol = kind.Subject, Children = [nested] });
        var work = kind.Work;
        Assert.NotNull(work);
        var nestedWork = nested.Work;
        Assert.NotNull(nestedWork);

        CancelNode(kind);

        Assert.True(work.IsCancellationRequested);
        Assert.True(nestedWork.IsCancellationRequested);
    }

    [Fact]
    public async Task Cancelling_an_analysis_that_is_not_running_does_nothing()
    {
        var d = AppStateDriver.Opened();
        var kind = await RunningKindAsync(d);
        kind.Run = new AnalyzerRun.Stopped();

        CancelNode(kind);

        Assert.IsType<AnalyzerRun.Stopped>(kind.Run);
    }

    [Fact]
    public async Task An_analysis_not_started_is_neither_running_nor_done_and_can_start()
    {
        var kind = await NewKindAsync(AppStateDriver.Opened());

        Assert.False(kind.HasStarted);
        Assert.False(kind.IsDone);
        Assert.Null(kind.Work);
    }

    [Fact]
    public async Task An_analysis_running_has_started_and_is_not_done()
    {
        var kind = await RunningKindAsync(AppStateDriver.Opened());

        Assert.True(kind.HasStarted);
        Assert.False(kind.IsDone);
        Assert.NotNull(kind.Work);
    }

    [Fact]
    public async Task An_analysis_stopped_has_started_and_neither_runs_nor_is_done()
    {
        var kind = await RunningKindAsync(AppStateDriver.Opened());

        CancelNode(kind);

        Assert.IsType<AnalyzerRun.Stopped>(kind.Run);
        Assert.True(kind.HasStarted);
        Assert.False(kind.IsDone);
        Assert.Null(kind.Work);
    }

    [Fact]
    public async Task An_analysis_done_has_started_and_no_longer_runs()
    {
        var kind = await RunningKindAsync(AppStateDriver.Opened());
        kind.Run = new AnalyzerRun.Done();

        Assert.True(kind.HasStarted);
        Assert.True(kind.IsDone);
        Assert.Null(kind.Work);
    }

    private static AnalyzerRows NewRows()
    {
        return new AnalyzerRows { AppCommands = new Queue<AppCommand>() };
    }

    [Fact]
    public async Task A_symbol_row_lists_what_can_be_asked_of_it()
    {
        var d = AppStateDriver.Opened();
        var rows = NewRows();
        var analyzed = (await FirstAsync(d)).Analysis;
        Assert.NotNull(analyzed);
        var symbol = new AnalyzerSymbolNode { Symbol = analyzed };

        Assert.True(rows.HasChildren(symbol));
        Assert.NotEmpty(rows.ListChildren(symbol));
        Assert.Equal(symbol.Symbol.Label, rows.Row(symbol).Label);
    }

    [Fact]
    public async Task An_analysis_row_lists_a_loading_row_and_asks_once_to_run()
    {
        var d = AppStateDriver.Opened();
        var rows = NewRows();
        var kind = await NewKindAsync(d);

        Assert.True(rows.HasChildren(kind));
        Assert.Same(rows.Loading, Assert.Single(rows.ListChildren(kind)));
        rows.ListChildren(kind);

        Assert.Single(rows.AppCommands);
    }

    [Fact]
    public void A_message_row_has_no_children_and_tells_why()
    {
        var rows = NewRows();
        var message = new AnalyzerMessageNode { Error = "failed" };

        Assert.False(rows.HasChildren(message));
        Assert.Empty(rows.ListChildren(message));
        Assert.Equal("failed", rows.Row(message).Label);
    }

    [Fact]
    public void A_root_waiting_for_its_symbol_has_no_children_and_shows_as_loading()
    {
        var rows = NewRows();
        var pending = new AnalyzerPendingNode { SavedAs = null };

        Assert.False(rows.HasChildren(pending));
        Assert.Empty(rows.ListChildren(pending));
        Assert.True(rows.Row(pending).Spinning);
    }

    [Fact]
    public void Cancelling_a_message_row_has_nothing_to_stop()
    {
        CancelNode(new AnalyzerMessageNode { Error = "failed" });
    }

    [Fact]
    public void Cancelling_a_root_still_waiting_for_its_symbol_has_nothing_to_stop()
    {
        CancelNode(new AnalyzerPendingNode { SavedAs = null });
    }

    [Fact]
    public async Task Its_menu_clears_the_analyzer()
    {
        var app = AppDriver.Opened(height: 600f);
        var d = app.Driver;
        d.Analyze(await FirstAsync(d));
        app.Run(3);
        app.Frames.ClickOn(GetAnalyzerRow("First() : int"), PointerButton.Right).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Clear")).Frame(2);

        Assert.Empty(d.AppState.Analyzer.Roots);
    }

    [Fact]
    public async Task The_toolbar_expands_and_runs_every_analysis_of_the_roots_then_collapses_them()
    {
        var app = AppDriver.Opened(height: 600f);
        var d = app.Driver;
        var analyzer = d.AppState.Analyzer;
        d.Analyze(await FirstAsync(d));
        app.Run(3);
        var root = analyzer.Roots[0];
        var rows = app.Ui.Analyzer;
        var kinds = rows.ListChildren(root).OfType<AnalyzerKindNode>().ToList();

        app.Frames.ClickOn(Selector.AtPath("dock/analyzer/expand")).Frame();
        var expanded = kinds.All(k => rows.View.IsExpanded(k));
        d.RunUntil(() => kinds.All(k => k.IsDone));
        app.Frames.ClickOn(Selector.AtPath("dock/analyzer/collapse")).Frame();

        Assert.NotEmpty(kinds);
        Assert.True(expanded);
        Assert.False(rows.View.IsExpanded(root));
    }

    [Fact]
    public async Task The_toolbar_removes_the_analysis_the_selection_is_in_or_every_analysis()
    {
        var app = AppDriver.Opened(height: 600f);
        var d = app.Driver;
        var analyzer = d.AppState.Analyzer;
        d.Analyze(await FirstAsync(d));
        var caller = (AnalyzerSymbolNode)Expand(d, analyzer.Found()[0], "Used By").Found[0];
        d.Analyze(await d.FixtureAsync("Greeter"));
        app.Ui.Analyzer.View.Select(caller);
        app.Run(3);

        app.Frames.ClickOn(Selector.AtPath("dock/analyzer/remove")).Frame(2);
        var left = analyzer.Found().Select(r => r.Symbol.Label).ToList();
        app.Frames.ClickOn(Selector.AtPath("dock/analyzer/clear")).Frame(2);

        Assert.Equal(["Greeter"], left);
        Assert.Empty(analyzer.Roots);
    }

    [Fact]
    public async Task Delete_in_the_analyzer_removes_the_analysis_the_selection_is_in()
    {
        var app = AppDriver.Opened(height: 600f);
        var d = app.Driver;
        await ShowFirstCallerAsync(app);
        app.Frames.ClickOn(GetAnalyzerRow("First() : int", "Used By", "Third() : int")).Frame();

        app.Frames.Key(KeyboardKey.Delete).Frame(2);

        Assert.Empty(d.AppState.Analyzer.Roots);
    }
}

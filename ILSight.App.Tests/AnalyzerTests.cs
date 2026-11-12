using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Testing;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.CodeViewArea;
using ILSight.CommandArea;
using static ILSight.Analysis;
using static ILSight.App.Tests.BuiltInGlyphs;

namespace ILSight.App.Tests;

public sealed class AnalyzerTests
{
    private static BrowseNode First(AppStateDriver d)
    {
        return d.Fixture("Outline").Children.Single(c => c.Text.StartsWith("First()"));
    }

    private static AnalyzerKindNode Expand(AppStateDriver d, AnalyzerSymbolNode symbol, string header)
    {
        var rows = d.Ui.Analyzer;
        var kind = Analysis.ListChildren(rows, symbol).OfType<AnalyzerKindNode>().Single(k => Core.Analysis.Analyzers.GetHeader(k.Kind) == header);
        rows.View.SetExpanded(kind, true);
        rows.GetChildren(kind);
        d.RunUntil(() => kind.IsDone);
        return kind;
    }

    [Fact]
    public void Analyzing_a_member_docks_the_analyzer_and_its_analyses_find_what_uses_it()
    {
        var d = AppStateDriver.Opened();

        d.Analyze(First(d));
        var root = Assert.Single(d.AppState.Analyzer.Found());
        var usedBy = Expand(d, root, "Used By");

        Assert.True(d.Ui.Layout.Contains(DockPanels.Analyzer));
        Assert.StartsWith("First()", root.Symbol.Label);
        var caller = Assert.IsType<AnalyzerSymbolNode>(Assert.Single(usedBy.Found));
        Assert.StartsWith("Third()", caller.Symbol.Label);
    }

    [Fact]
    public void A_failed_analysis_tells_why_in_its_row_and_reports_the_whole_error()
    {
        var d = AppStateDriver.Opened();
        var elsewhere = new AssemblyList { Runtime = TestRuntime.Installed }.Open(AppStateDriver.FixturePath).Root.Child(AppStateDriver.FixturesNamespace, "Outline");
        var symbol = Core.Analysis.Analyzers.Of(elsewhere);
        Assert.NotNull(symbol);
        var kind = new AnalyzerKindNode { Subject = symbol, Kind = Core.Analysis.Analyzers.KindsFor(symbol).Single(k => Core.Analysis.Analyzers.GetHeader(k) == "Used By") };

        Commands.StartAnalysis(d.AppState, StartRun(kind));
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
    public void Used_By_a_property_or_event_finds_each_member_using_its_accessors_once(string member, string user)
    {
        var d = AppStateDriver.Opened();
        var node = d.Fixture("Counter").Children
            .Where(c => !c.IsCompilerGenerated)
            .Single(c => c.Text.StartsWith(member + " "));

        d.Analyze(node);
        var usedBy = Expand(d, Assert.Single(d.AppState.Analyzer.Found()), "Used By");

        var found = Assert.IsType<AnalyzerSymbolNode>(Assert.Single(usedBy.Found));
        Assert.StartsWith(user, found.Symbol.Label);
    }

    [Fact]
    public void Applied_To_finds_members_that_carry_a_security_attribute()
    {
        var d = AppStateDriver.Opened();

        d.Analyze(d.Fixture("GuardAttribute"));
        var appliedTo = Expand(d, Assert.Single(d.AppState.Analyzer.Found()), "Applied To");

        var member = Assert.IsType<AnalyzerSymbolNode>(Assert.Single(appliedTo.Found));
        Assert.StartsWith("Enter()", member.Symbol.Label);
    }

    [Fact]
    public void A_result_opens_its_member_when_activated_and_can_be_analyzed_in_turn()
    {
        var app = AppDriver.Opened(height: 600f);
        var d = app.Driver;
        d.Analyze(First(d));
        var analyzer = d.AppState.Analyzer;
        var caller = (AnalyzerSymbolNode)Expand(d, analyzer.Found()[0], "Used By").Found[0];
        app.Ui.Analyzer.View.Select(caller);
        app.Run(3);

        app.DoubleClickOn(Selector.WithText(app.Frames.Canvas.Texts.Last(t => t.Inputs.Text.StartsWith("Third()")).Inputs.Text));
        d.Settle();
        AnalyzerPanelView.Analyze(app.Ui.Analyzer, app.AppState, caller.Symbol);

        Assert.StartsWith("Third()", d.AppState.Navigation.ActiveTab?.Node?.Text);
        Assert.Equal(2, analyzer.Roots.Count);
        Assert.Same(analyzer.Roots[1], app.Ui.Analyzer.View.Selected);
    }

    [Fact]
    public void Ctrl_R_analyzes_the_node_selected_in_the_focused_tree()
    {
        var app = AppDriver.Opened();
        var d = app.Driver;
        Trees.RevealNode(d.Ui.Tree, First(d));
        app.Run(2);
        app.Frames.ClickOn(GetFixtureRow("Outline", First(d).Text));
        d.Settle();

        app.Frames.Key(Shortcuts.Analyze).Frame(2);

        Assert.StartsWith("First()", Assert.Single(d.AppState.Analyzer.Found()).Symbol.Label);
    }

    [Fact]
    public void Ctrl_R_in_the_code_analyzes_the_reference_at_the_caret()
    {
        var view = CodeViewDriver.Showing(CodeViewDriver.Outline(), 1200f, frames: 2);
        var (line, column) = view.Document.GetPosition("return First();");

        view.Click(line, column + "return First".Length);
        view.Key(Shortcuts.Analyze);

        Assert.Equal("First", Assert.IsType<CodeViewIntent.Analyze>(view.Last.Intent).Entity.Name);
    }

    [Fact]
    public void Ctrl_R_in_the_analyzer_analyzes_the_result_it_has_selected()
    {
        var app = AppDriver.Opened(height: 600f);
        var d = app.Driver;
        ShowFirstCaller(app);
        app.Frames.ClickOn(GetAnalyzerRow("First() : int", "Used By", "Third() : int")).Frame();

        app.Frames.Key(Shortcuts.Analyze).Frame(2);

        Assert.Equal(2, d.AppState.Analyzer.Roots.Count);
        Assert.StartsWith("Third()", d.AppState.Analyzer.Found()[1].Symbol.Label);
    }

    private static void ShowFirstCaller(AppDriver app)
    {
        var d = app.Driver;
        var analyzer = d.AppState.Analyzer;
        d.Analyze(First(d));
        var caller = Expand(d, analyzer.Found()[0], "Used By").Found[0];
        d.Ui.Analyzer.View.ScrollIntoView(caller);
        app.Run(3);
    }


    [Fact]
    public void A_reference_in_the_code_is_analyzed_once_it_is_found()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        d.Show(d.Fixture("Outline"));
        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var document = tab.Document;
        var (line, column) = document.GetPosition("return First();");
        var reference = Assert.IsAssignableFrom<CodeReference.Navigable>(document.SpanAt(line, column + "return ".Length).Reference);
        d.TakeViewCommands();

        Commands.StartAnalyzing(d.AppState, BeginAnalyzing(d.AppState.Analyzer, reference));
        d.Settle();

        var root = Assert.Single(d.AppState.Analyzer.Found());
        Assert.StartsWith("First()", root.Symbol.Label);
        Assert.Equal([new ViewCommand.ShowAnalysis(root)], d.TakeViewCommands());
    }

    private static CodeReference.Entity ReferenceTo(AppStateDriver d, string type, string text, int offset = 0)
    {
        d.Show(d.Fixture(type));
        var tab = d.AppState.Navigation.ActiveTab;
        Assert.NotNull(tab);
        var document = tab.Document;
        var (line, column) = document.GetPosition(text);
        return Assert.IsType<CodeReference.Entity>(document.SpanAt(line, column + offset).Reference);
    }

    private static CodeReference.Entity ReferenceToFirst(AppStateDriver d)
    {
        return ReferenceTo(d, "Outline", "return First();", "return ".Length);
    }

    [Fact]
    public void A_reference_shows_a_loading_root_at_once_that_is_saved_once_it_is_found()
    {
        var d = AppStateDriver.Opened();
        var reference = ReferenceToFirst(d);
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
    public void Two_analyses_asked_for_by_reference_both_add_their_roots_in_the_order_asked()
    {
        var d = AppStateDriver.Opened();
        var length = ReferenceTo(d, "Greeter", "Length");
        var greet = ReferenceTo(d, "Greeter", "Greet(2)");

        d.Analyze(length);
        d.Analyze(greet);
        d.Settle();

        var labels = d.AppState.Analyzer.Found().Select(r => r.Symbol.Label).ToList();
        Assert.Equal(2, labels.Count);
        Assert.StartsWith("Length", labels[0]);
        Assert.StartsWith("Greet(", labels[1]);
    }

    [Fact]
    public void A_reference_to_a_symbol_analyzed_already_selects_its_root()
    {
        var d = AppStateDriver.OpenedWithoutScreen();
        var reference = ReferenceToFirst(d);
        var analyzer = d.AppState.Analyzer;
        var first = AddToAnalyzer(d.AppState, First(d));
        AddToAnalyzer(d.AppState, d.Fixture("Greeter"));
        d.TakeViewCommands();

        Commands.StartAnalyzing(d.AppState, BeginAnalyzing(analyzer, reference));
        d.Settle();

        Assert.Equal(2, analyzer.Roots.Count);
        Assert.Same(analyzer.Found()[0], first);
        Assert.NotNull(first);
        Assert.Equal([new ViewCommand.ShowAnalysis(first)], d.TakeViewCommands());
    }

    [Fact]
    public void A_reference_whose_root_was_removed_meanwhile_is_dropped()
    {
        var d = AppStateDriver.Opened();
        var reference = ReferenceToFirst(d);
        var analyzer = d.AppState.Analyzer;

        d.Analyze(reference);
        analyzer.RemoveRoot(analyzer.Roots[0]);
        d.Settle();

        Assert.Empty(analyzer.Roots);
    }

    [Fact]
    public void A_reference_that_does_not_resolve_removes_its_root_and_is_reported()
    {
        using var temp = new TempDir();
        var copy = temp.Write("Copy.dll", File.ReadAllBytes(AppStateDriver.FixturePath));
        var d = new AppStateDriver();
        d.Start((a, ct) => a.AssemblyOpening.OpenAssembliesAsync(a, [AssemblyPath.Of(copy)], ct));
        d.RunUntil(() => d.AppState.Assemblies.Assemblies.Count == 1);
        d.Settle();
        var assembly = d.AppState.Assemblies.Assemblies.Single();
        var outline = assembly.Root.Child(AppStateDriver.FixturesNamespace, "Outline");
        var document = outline.Decompile(DecompilationLanguage.CSharp);
        var (line, column) = document.GetPosition("return First();");
        var reference = Assert.IsAssignableFrom<CodeReference.Navigable>(document.SpanAt(line, column + "return ".Length).Reference);
        d.AppState.ListSession.CloseAssemblies([assembly]);
        File.Delete(copy);

        d.Analyze(reference);
        d.Settle();

        Assert.Empty(d.AppState.Analyzer.Roots);
        Assert.Equal("Cannot resolve First", Assert.Single(d.AppState.Errors.Entries).Words().Title);
    }

    [Fact]
    public void Removing_a_root_or_clearing_the_assembly_list_empties_the_analyzer()
    {
        var d = AppStateDriver.Opened();
        var analyzer = d.AppState.Analyzer;
        d.Analyze(First(d));
        d.Analyze(d.Fixture("Greeter"));

        analyzer.RemoveRoot(analyzer.Roots[0]);
        var left = analyzer.Found().Select(r => r.Symbol.Label).ToList();
        d.Change(static a => a.ListSession.CloseAllAssemblies());

        Assert.Equal(["Greeter"], left);
        Assert.Empty(analyzer.Roots);
    }

    [Fact]
    public void Removing_a_root_leaves_the_analyses_of_results_never_expanded_unlisted()
    {
        var d = AppStateDriver.Opened();
        var analyzer = d.AppState.Analyzer;
        d.Analyze(First(d));
        var root = analyzer.Found()[0];
        var caller = (AnalyzerSymbolNode)Expand(d, root, "Used By").Found[0];

        analyzer.RemoveRoot(root);

        Assert.Null(caller.Children);
    }

    private static AnalyzerKindNode NewKind(AppStateDriver d)
    {
        var symbol = Core.Analysis.Analyzers.Of(First(d));
        Assert.NotNull(symbol);
        return new AnalyzerKindNode { Subject = symbol, Kind = Core.Analysis.Analyzers.KindsFor(symbol)[0] };
    }

    private static AnalyzerKindNode RunningKind(AppStateDriver d)
    {
        var kind = NewKind(d);
        kind.Run = new AnalyzerRun.Running(new CancellationTokenSource());
        return kind;
    }

    [Fact]
    public void Cancelling_a_symbol_cancels_the_analyses_listed_under_it()
    {
        var d = AppStateDriver.Opened();
        var running = RunningKind(d);
        var work = running.Work;
        Assert.NotNull(work);
        var symbol = new AnalyzerSymbolNode { Symbol = running.Subject, Children = [running] };

        CancelNode(symbol);

        Assert.True(work.IsCancellationRequested);
        Assert.Null(running.Work);
    }

    [Fact]
    public void Cancelling_a_symbol_whose_children_were_never_listed_leaves_them_unlisted()
    {
        var d = AppStateDriver.Opened();
        var analyzed = Core.Analysis.Analyzers.Of(First(d));
        Assert.NotNull(analyzed);
        var symbol = new AnalyzerSymbolNode { Symbol = analyzed };

        CancelNode(symbol);

        Assert.Null(symbol.Children);
    }

    [Fact]
    public void Cancelling_an_analysis_cancels_its_run_and_the_analyses_under_what_it_found()
    {
        var d = AppStateDriver.Opened();
        var kind = RunningKind(d);
        var nested = RunningKind(d);
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
    public void Cancelling_an_analysis_that_is_not_running_does_nothing()
    {
        var d = AppStateDriver.Opened();
        var kind = RunningKind(d);
        kind.Run = new AnalyzerRun.Stopped();

        CancelNode(kind);

        Assert.IsType<AnalyzerRun.Stopped>(kind.Run);
    }

    [Fact]
    public void An_analysis_not_started_is_neither_running_nor_done_and_can_start()
    {
        var kind = NewKind(AppStateDriver.Opened());

        Assert.False(kind.HasStarted);
        Assert.False(kind.IsDone);
        Assert.Null(kind.Work);
    }

    [Fact]
    public void An_analysis_running_has_started_and_is_not_done()
    {
        var kind = RunningKind(AppStateDriver.Opened());

        Assert.True(kind.HasStarted);
        Assert.False(kind.IsDone);
        Assert.NotNull(kind.Work);
    }

    [Fact]
    public void An_analysis_stopped_has_started_and_neither_runs_nor_is_done()
    {
        var kind = RunningKind(AppStateDriver.Opened());

        CancelNode(kind);

        Assert.IsType<AnalyzerRun.Stopped>(kind.Run);
        Assert.True(kind.HasStarted);
        Assert.False(kind.IsDone);
        Assert.Null(kind.Work);
    }

    [Fact]
    public void An_analysis_done_has_started_and_no_longer_runs()
    {
        var kind = RunningKind(AppStateDriver.Opened());
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
    public void A_symbol_row_lists_what_can_be_asked_of_it()
    {
        var d = AppStateDriver.Opened();
        var rows = NewRows();
        var analyzed = Core.Analysis.Analyzers.Of(First(d));
        Assert.NotNull(analyzed);
        var symbol = new AnalyzerSymbolNode { Symbol = analyzed };

        Assert.True(rows.HasChildren(symbol));
        Assert.NotEmpty(ListChildren(rows, symbol));
        Assert.Equal(symbol.Symbol.Label, rows.Row(symbol).Label);
    }

    [Fact]
    public void An_analysis_row_lists_a_loading_row_and_asks_once_to_run()
    {
        var d = AppStateDriver.Opened();
        var rows = NewRows();
        var kind = NewKind(d);

        Assert.True(rows.HasChildren(kind));
        Assert.Same(rows.Loading, Assert.Single(ListChildren(rows, kind)));
        ListChildren(rows, kind);

        Assert.Single(rows.AppCommands);
    }

    [Fact]
    public void A_message_row_has_no_children_and_tells_why()
    {
        var rows = NewRows();
        var message = new AnalyzerMessageNode { Error = "failed" };

        Assert.False(rows.HasChildren(message));
        Assert.Empty(ListChildren(rows, message));
        Assert.Equal("failed", rows.Row(message).Label);
    }

    [Fact]
    public void A_root_waiting_for_its_symbol_has_no_children_and_shows_as_loading()
    {
        var rows = NewRows();
        var pending = new AnalyzerPendingNode { SavedAs = null };

        Assert.False(rows.HasChildren(pending));
        Assert.Empty(ListChildren(rows, pending));
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
    public void Its_menu_clears_the_analyzer()
    {
        var app = AppDriver.Opened(height: 600f);
        var d = app.Driver;
        d.Analyze(First(d));
        app.Run(3);
        app.Frames.ClickOn(GetAnalyzerRow("First() : int"), PointerButton.Right).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, "Clear")).Frame(2);

        Assert.Empty(d.AppState.Analyzer.Roots);
    }

    [Fact]
    public void The_toolbar_expands_and_runs_every_analysis_of_the_roots_then_collapses_them()
    {
        var app = AppDriver.Opened(height: 600f);
        var d = app.Driver;
        var analyzer = d.AppState.Analyzer;
        d.Analyze(First(d));
        app.Run(3);
        var root = analyzer.Roots[0];
        var rows = app.Ui.Analyzer;
        var kinds = Analysis.ListChildren(rows, root).OfType<AnalyzerKindNode>().ToList();

        app.Frames.ClickOn(Selector.AtPath("dock/analyzer/expand")).Frame();
        var expanded = kinds.All(k => rows.View.IsExpanded(k));
        d.RunUntil(() => kinds.All(k => k.IsDone));
        app.Frames.ClickOn(Selector.AtPath("dock/analyzer/collapse")).Frame();

        Assert.NotEmpty(kinds);
        Assert.True(expanded);
        Assert.False(rows.View.IsExpanded(root));
    }

    [Fact]
    public void The_toolbar_removes_the_analysis_the_selection_is_in_or_every_analysis()
    {
        var app = AppDriver.Opened(height: 600f);
        var d = app.Driver;
        var analyzer = d.AppState.Analyzer;
        d.Analyze(First(d));
        var caller = (AnalyzerSymbolNode)Expand(d, analyzer.Found()[0], "Used By").Found[0];
        d.Analyze(d.Fixture("Greeter"));
        app.Ui.Analyzer.View.Select(caller);
        app.Run(3);

        app.Frames.ClickOn(Selector.AtPath("dock/analyzer/remove")).Frame(2);
        var left = analyzer.Found().Select(r => r.Symbol.Label).ToList();
        app.Frames.ClickOn(Selector.AtPath("dock/analyzer/clear")).Frame(2);

        Assert.Equal(["Greeter"], left);
        Assert.Empty(analyzer.Roots);
    }

    [Fact]
    public void Delete_in_the_analyzer_removes_the_analysis_the_selection_is_in()
    {
        var app = AppDriver.Opened(height: 600f);
        var d = app.Driver;
        ShowFirstCaller(app);
        app.Frames.ClickOn(GetAnalyzerRow("First() : int", "Used By", "Third() : int")).Frame();

        app.Frames.Key(KeyboardKey.Delete).Frame(2);

        Assert.Empty(d.AppState.Analyzer.Roots);
    }
}

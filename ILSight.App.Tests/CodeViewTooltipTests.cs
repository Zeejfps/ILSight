using ZGF.Gui.Immediate;
using ILSight.App.Tests.Fixtures;
using ILSight.Core;
using ILSight.CodeViewArea;
using ILSight.CodeTooltipArea;

namespace ILSight.App.Tests;

public sealed class CodeViewTooltipTests
{
    private const double RestSeconds = 2;
    private const float WindowHeight = 700f;
    private const double LeaveGraceSeconds = 0.3;
    private const string Context = "ILSight.App.Tests.Fixtures.Farewell";

    private static async Task<CodeViewDriver> FarewellAsync()
    {
        var list = new AssemblyList { Runtime = TestRuntime.Installed };
        var type = typeof(Farewell);
        Assert.NotNull(type.Namespace);
        var node = await list.Open(type.Assembly.Location).ReadRootAsync().GetChildAsync(type.Namespace, type.Name);
        var view = new CodeViewDriver(await node.DecompileAsync(DecompilationLanguage.CSharp), WindowHeight) { Tooltips = new CodeTooltips { Assemblies = list, Version = list.Version } };
        view.Run();
        return view;
    }

    /// <summary>Runs the hover out: its delay, the lookup it starts in the background, and the delay before the
    /// tooltip shows what the lookup found; then <paramref name="text"/> is drawn.</summary>
    private static void RunUntilDrawn(CodeViewDriver view, string text)
    {
        RunOut(view);
        Assert.True(view.Frames.Drew(text), $"'{text}' was not drawn");
    }

    /// <summary>Runs the hover out: its delay, any lookup it starts in the background, and the delay before the
    /// tooltip shows what the lookup found.</summary>
    private static void RunOut(CodeViewDriver view)
    {
        view.Frames.Settle(SettleSeconds);
        view.Frames.WaitUntilReady(Patience);
        view.Frames.Settle(SettleSeconds);
    }

    /// <summary>Whether a tooltip is drawn: text in the tooltip's text size, not the code's.</summary>
    private static bool IsTooltipDrawn(CodeViewDriver view)
    {
        return view.Frames.Canvas.Texts.Any(static t => t.Inputs.Style.FontSize.Value != UiTheme.Dark.MonoFontSize);
    }

    private static void RestOn(CodeViewDriver view, string text)
    {
        var (line, column) = view.Document.GetPosition(text);
        view.MoveTo(line, column + 1);
    }

    [Fact]
    public async Task Resting_on_a_member_in_a_closed_assemblys_code_says_it_was_closed_and_looks_nothing_up()
    {
        var view = await FarewellAsync();
        view.ClosedAssembly = "Shop.dll";

        RestOn(view, "Say()");
        RunOut(view);

        Assert.True(view.Frames.Drew("Shop.dll was closed."));
        Assert.False(view.Frames.Drew("Says "));
        Assert.False(view.Frames.Drew("Bye"));
    }

    [Fact]
    public async Task Resting_on_a_member_shows_its_declaration_and_documentation()
    {
        var view = await FarewellAsync();

        RestOn(view, "Say()");

        RunUntilDrawn(view, "Says ");
        Assert.True(view.Frames.Drew("Bye"));
        Assert.True(view.Frames.Drew(Context));
        var summary = view.Frames.Canvas.Texts.Single(t => t.Inputs.Text == "Says ");
        var code = view.Frames.Canvas.Texts.Single(t => t.Inputs.Text == "Bye");
        Assert.NotEqual(MonoFonts.Code, summary.Inputs.Style.FontFamily.Value);
        Assert.Equal(MonoFonts.Code, code.Inputs.Style.FontFamily.Value);
    }

    [Fact]
    public async Task A_signature_too_wide_for_one_line_puts_each_parameter_on_a_line_of_its_own()
    {
        var view = await FarewellAsync();

        RestOn(view, "Compose(");
        RunUntilDrawn(view, Context);
        var compose = TooltipText(view, "Compose");
        var greeting = TooltipText(view, "greeting");
        var name = TooltipText(view, "name");

        Assert.True(compose.Y < greeting.Y);
        Assert.True(greeting.Y < name.Y);
        Assert.Equal(greeting.X, name.X);
    }

    /// <summary>Where the tooltip drew <paramref name="text"/>: in the tooltip's text size, not the code's.</summary>
    private static UiRect TooltipText(CodeViewDriver view, string text)
    {
        var drawn = view.Frames.Canvas.Texts
            .Where(t => t.Inputs.Text == text)
            .Single(t => t.Inputs.Style.FontSize.Value != UiTheme.Dark.MonoFontSize);
        return view.Frames.ToUi(drawn.Inputs.Position);
    }

    [Fact]
    public async Task Clicking_a_type_in_the_tooltip_navigates_to_it_and_closes_the_tooltip()
    {
        var view = await FarewellAsync();
        RestOn(view, "Say()");
        RunUntilDrawn(view, "Says ");

        var type = TooltipText(view, "Farewell");
        var result = view.Click((type.X + 2f, type.Y + type.Height / 2));
        view.Run(2);

        var navigate = Assert.IsType<CodeViewIntent.Navigate>(result.Intent);
        Assert.Equal(EntityKind.Type, Assert.IsType<CodeReference.Entity>(navigate.Target).Kind);
        Assert.Equal("Farewell", navigate.Target.Name);
        Assert.False(navigate.InNewTab);
        Assert.False(view.Frames.Drew("Says "));
    }

    [Fact]
    public async Task The_tooltip_waits_for_the_pointer_to_rest_and_closes_when_it_leaves()
    {
        var view = await FarewellAsync();
        RestOn(view, "Say()");
        RunUntilDrawn(view, "Says ");
        var shown = IsTooltipDrawn(view);

        view.MoveTo(0, 0);
        view.Frames.Wait(LeaveGraceSeconds).Frame(2);

        Assert.True(shown);
        Assert.False(view.Frames.Drew("Says "));
        Assert.False(IsTooltipDrawn(view));
    }

    [Fact]
    public async Task A_keyword_has_no_tooltip()
    {
        var view = await FarewellAsync();

        RestOn(view, "public class");
        view.Frames.Wait(RestSeconds).Frame(2);
        RunOut(view);

        Assert.False(IsTooltipDrawn(view));
    }
}

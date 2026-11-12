using ZGF.Gui.Immediate;
using ILSight.Core;

namespace ILSight.App.Tests;

public sealed class ReferenceTreeTests
{
    [Fact]
    public void A_reference_of_a_reference_shown_is_revealed_in_the_tree_though_its_assembly_is_not_listed()
    {
        var app = AppDriver.Opened();
        var coreLib = app.Node("References", "System.Runtime", "System.Private.CoreLib");

        app.Show(coreLib, 2);
        app.Settle(2);

        Assert.DoesNotContain(coreLib.Assembly, app.AppState.Assemblies.Listed);
        Assert.Same(coreLib, app.Ui.Tree.View.Selected);
        Assert.Same(coreLib, app.AppState.Tabs.Active?.Node);
    }

    [Fact]
    public void The_frame_go_to_target_is_picked_in_says_the_app_is_resolving()
    {
        var app = AppDriver.Opened();
        var greeter = app.Fixture("Greeter");
        var baseType = greeter.Child("Base Types").Children[0];
        app.Show(greeter, 3);
        Trees.RevealNode(app.Ui.Tree, baseType);
        app.Run(2);

        app.Frames.ClickOn(Selector.OfRole(SemanticRole.TreeItem, baseType.Text), PointerButton.Right).Frame();
        app.Frames.ClickOn(Selector.OfRole(SemanticRole.MenuItem, app.Ui.Strings.TreeGoToTarget));

        Assert.True(app.Frames.Drew(app.Ui.Strings.StatusResolving));
        app.Settle(3);
        Assert.False(app.AppState.IsResolving);
        var active = app.AppState.Tabs.Active;
        Assert.NotNull(active);
        Assert.NotNull(active.Node);
        Assert.NotSame(greeter, active.Node);
    }
}

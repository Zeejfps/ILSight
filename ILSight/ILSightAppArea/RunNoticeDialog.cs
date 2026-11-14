using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;

namespace ILSight.ILSightAppArea;

/// <summary>A small window of its own that says one thing and waits for a click: what a run with no console and no
/// window of the app shows the user (<see cref="RunNotice.InDialog"/>).</summary>
[Edge]
internal sealed class RunNoticeDialog(string message, string ok) : ImmediateGuiApp<DefaultUiState>(new DefaultUiState())
{
    /// <summary>Shows <paramref name="message"/> with a button labelled <paramref name="ok"/>, returning once the user
    /// clicked it or closed the window.</summary>
    public static void Show(string message, string ok)
    {
        var config = new StartupConfig
        {
            WindowWidth = 480,
            WindowHeight = 180,
            WindowTitle = "ILSight",
        };
        var host = new ImmediateGuiAppHost<DefaultUiState>(new RunNoticeDialog(message, ok), config);
        host.Run();
    }

    protected override void OnUpdate(Ui<DefaultUiState> ui)
    {
        using var screen = ui.Screen();
        var theme = ui.Theme;
        using (ui.Column(new Box { Width = Sizing.Grow(), Height = Sizing.Grow(), Padding = new Spacing(theme.PanelPadding), Gap = theme.Gap }))
        {
            ui.Text(message, wrap: true);
            ui.Spacer();
            using (ui.Row())
            {
                ui.Spacer();
                if (ui.Button(ok, id: "ok", variant: ButtonVariant.Primary))
                {
                    ui.Quit();
                }
            }
        }
    }
}

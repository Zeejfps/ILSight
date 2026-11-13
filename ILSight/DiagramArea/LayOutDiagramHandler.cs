using ILSight.Core.Diagrams;

namespace ILSight.DiagramArea;

/// <summary>Lays out a diagram (<see cref="TypeDiagram.StartLayout"/>) through <see cref="IPorts"/>, without holding up
/// the screen, and holds the layout unless another was started meanwhile or the diagram was closed.</summary>
public sealed class LayOutDiagramHandler(LayOutDiagramHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user a diagram could not be laid out.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Where cards and labels as large as <paramref name="measures"/> says go, worked out without holding
        /// up the screen. Working it out that failed comes back as <see cref="Outcome{T}.Failed"/>; the cancellation
        /// of <paramref name="ct"/> is let through.</summary>
        Task<Outcome<TypeLayout>> ArrangeAsync(DiagramMeasures measures, CancellationToken ct);
    }

    /// <summary>Lays out <paramref name="diagram"/>, its cards and labels as large as <paramref name="measures"/>
    /// says, as the work <paramref name="layout"/>, and holds the layout unless another was started meanwhile or
    /// the diagram was closed; reports a layout that failed. <paramref name="ct"/> is the workflow's token, which
    /// the edge that starts it links to <paramref name="layout"/>.</summary>
    public async Task HandleAsync(TypeDiagram diagram, WorkTicket layout, DiagramMeasures measures, CancellationToken ct)
    {
        var laidOut = await ports.ArrangeAsync(measures, ct);
        if (!diagram.TryFinishLayout(layout))
        {
            return;
        }

        laidOut.Switch(
            ok => diagram.ShowLayout(ok.Value),
            failed => ports.ReportError(new AppProblem.DiagramNotLaidOut(failed.Error)));
    }
}

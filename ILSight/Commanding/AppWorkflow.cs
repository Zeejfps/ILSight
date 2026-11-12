namespace ILSight.Commanding;

/// <summary>What the screen asks to run in the background over the app state: a workflow that
/// <see cref="Commands.Start(UiState, string, AppWorkflow)"/> queues with the <see cref="AppCommand"/>s, which
/// <see cref="WorkflowRunner"/> starts in its place among them once the frame is drawn. <paramref name="ct"/> fires
/// when the app shuts down.</summary>
public delegate Task AppWorkflow(AppState appState, CancellationToken ct);

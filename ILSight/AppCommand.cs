using ILSight.CommandArea;
using ILSight.ILSightAppArea;

namespace ILSight;

/// <summary>What the screen asks of the app state, which <see cref="UiState"/> queues while it draws
/// and <see cref="ILSightApp"/> runs, in the order queued, once the frame is drawn. Made through
/// <see cref="UiState.Later(AppCommand)"/> and its overload, from a <c>static</c> lambda handed what it
/// acts on, so that a view drawn every frame allocates nothing until it asks. Asking for an async workflow
/// queues one too, which starts it (<see cref="Commands.Start(UiState, string, AppWorkflow)"/>), so commands and
/// workflow starts run in the order they were asked for.</summary>
public delegate void AppCommand(AppState appState);

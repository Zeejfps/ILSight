namespace ILSight;

/// <summary>Looks for a newer version of the app in the background (<see cref="AppUpdates"/>) without the user
/// asking, as the app starts.</summary>
[Edge]
public sealed class UpdateChecker
{
    private readonly WorkflowRunner _workflows;
    private readonly AppUpdates _updates;

    /// <param name="workflows">Runs the check.</param>
    /// <param name="updates">What the app knows of its updates.</param>
    internal UpdateChecker(WorkflowRunner workflows, AppUpdates updates)
    {
        _workflows = workflows;
        _updates = updates;
    }

    /// <summary>Starts looking for a newer version when <paramref name="isAutomatic"/>, the user's choice, says to and
    /// one can be looked for now (<see cref="AppUpdates.CanCheck"/>).</summary>
    public void CheckIfAutomatic(bool isAutomatic)
    {
        if (!isAutomatic)
        {
            return;
        }

        var check = _updates.StartCheck(asked: false);
        if (check is null)
        {
            return;
        }

        StartChecking(check);
    }

    // A method of its own, so that the calls that start nothing allocate no closure.
    private void StartChecking(UpdateCheck check)
    {
        var updates = _updates;
        _workflows.StartSettling(nameof(AppUpdates.CheckAsync), ct => updates.CheckAsync(check, ct), _ => updates.FailCheck(check));
    }
}

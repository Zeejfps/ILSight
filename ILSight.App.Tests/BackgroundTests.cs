using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILSight.App.Tests;

public sealed class BackgroundTests
{
    [Fact]
    public void No_async_void()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var asyncVoid = typeof(Background).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(all))
            .Where(m => m.ReturnType == typeof(void) && m.GetCustomAttribute<AsyncStateMachineAttribute>() is not null)
            .Select(m => $"{m.DeclaringType?.FullName}.{m.Name}")
            .ToList();
        Assert.Empty(asyncVoid);
    }

    [Fact]
    public async Task A_workflow_that_throws_after_awaiting_is_reported_as_unexpected()
    {
        var work = new BackgroundWork();
        var errors = new ErrorLog();
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        new WorkflowRunner(work, errors).Start("failing", _ => FailAsync(resume.Task));

        resume.SetResult();
        await WorkflowRunnerTests.WaitUntilEndedAsync(work);

        var problem = Assert.IsType<AppProblem.Unexpected>(Assert.Single(errors.Entries).Problem);
        Assert.IsType<InvalidOperationException>(problem.Error);
        Assert.Equal(Strings.En.ErrorUnexpected, ProblemTexts.Describe(Strings.En, problem).Title);
    }

    private static async Task FailAsync(Task resume)
    {
        await resume;
        throw new InvalidOperationException("after the await");
    }
}

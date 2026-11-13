using ILSight.ExportArea;

namespace ILSight.App.Tests;

public sealed class CheckExportFilesHandlerTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "exported");

    private static readonly string KeyFile = Path.Combine(Path.GetTempPath(), "signing.snk");

    [Fact]
    public async Task HandleAsync_FilledFolderAndMissingKeyFile_SaysSo()
    {
        var checks = new ExportFileChecks();
        var check = checks.StartCheck(" " + Folder + " ", KeyFile);
        Assert.NotNull(check);
        var ports = new FakeCheckExportFilesHandlerPorts { FilledFolders = [Folder] };

        await new CheckExportFilesHandler(ports).HandleAsync(checks, check, CancellationToken.None);

        var facts = checks.FindFacts(" " + Folder + " ", KeyFile);
        Assert.Equal(new ExportFileFacts { FolderHoldsFiles = true, IsKeyFileMissing = true }, facts);
        Assert.Equal([Folder, KeyFile], ports.Asked);
    }

    [Fact]
    public async Task HandleAsync_FolderCannotBeListed_FailsTheLookSayingWhyAndAsksNothingMoreUntilRetried()
    {
        var checks = new ExportFileChecks();
        var check = checks.StartCheck(Folder, KeyFile);
        Assert.NotNull(check);
        var ports = new FakeCheckExportFilesHandlerPorts { Unlistable = { [Folder] = new UnauthorizedAccessException("Access is denied.") } };

        await new CheckExportFilesHandler(ports).HandleAsync(checks, check, CancellationToken.None);

        Assert.Equal("Access is denied.", Assert.IsType<ExportFileLook.Failed>(checks.FindLook(Folder, KeyFile)).Why);
        Assert.Null(checks.FindFacts(Folder, KeyFile));
        Assert.Null(checks.StartCheck(Folder, KeyFile));
    }

    [Fact]
    public async Task Retry_AfterAFailedLook_LooksAtTheSameNamesAgain()
    {
        var checks = new ExportFileChecks();
        var failing = checks.StartCheck(Folder, "");
        Assert.NotNull(failing);
        await new CheckExportFilesHandler(new FakeCheckExportFilesHandlerPorts { Unlistable = { [Folder] = new IOException("The network path was not found.") } })
            .HandleAsync(checks, failing, CancellationToken.None);

        var retried = checks.Retry();
        Assert.NotNull(retried);
        var lookWhileRetried = checks.FindLook(Folder, "");
        await new CheckExportFilesHandler(new FakeCheckExportFilesHandlerPorts { FilledFolders = [Folder] }).HandleAsync(checks, retried, CancellationToken.None);

        Assert.True(retried.IsOf(Folder, ""));
        Assert.IsType<ExportFileLook.Checking>(lookWhileRetried);
        Assert.Equal(new ExportFileFacts { FolderHoldsFiles = true, IsKeyFileMissing = false }, checks.FindFacts(Folder, ""));
        Assert.Null(checks.Retry());
    }

    [Fact]
    public async Task HandleAsync_RelativeFolderAndNoKeyFile_LooksAtNothing()
    {
        var checks = new ExportFileChecks();
        var check = checks.StartCheck("relative", "");
        Assert.NotNull(check);
        var ports = new FakeCheckExportFilesHandlerPorts { Files = [KeyFile] };

        await new CheckExportFilesHandler(ports).HandleAsync(checks, check, CancellationToken.None);

        Assert.Equal(new ExportFileFacts { FolderHoldsFiles = false, IsKeyFileMissing = false }, checks.FindFacts("relative", ""));
        Assert.Empty(ports.Asked);
    }

    [Fact]
    public async Task HandleAsync_NamesChangedMeanwhile_TakesNothing()
    {
        var checks = new ExportFileChecks();
        var older = checks.StartCheck(Folder, "");
        Assert.NotNull(older);
        var newer = checks.StartCheck(Folder, KeyFile);
        Assert.NotNull(newer);
        var ports = new FakeCheckExportFilesHandlerPorts { FilledFolders = [Folder], Files = [KeyFile] };

        await new CheckExportFilesHandler(ports).HandleAsync(checks, older, CancellationToken.None);

        Assert.Null(checks.FindFacts(Folder, ""));
        Assert.Null(checks.FindFacts(Folder, KeyFile));
    }

    [Fact]
    public void StartCheck_SameNamesAsked_StartsNothing()
    {
        var checks = new ExportFileChecks();
        var first = checks.StartCheck(Folder, "");
        Assert.NotNull(first);

        var again = checks.StartCheck(Folder, "");

        Assert.Null(again);
        Assert.True(checks.HasAsked(Folder, ""));
    }
}

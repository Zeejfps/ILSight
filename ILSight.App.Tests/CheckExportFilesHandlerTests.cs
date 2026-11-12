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

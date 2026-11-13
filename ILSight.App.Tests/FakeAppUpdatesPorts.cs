using Velopack;
using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>A feed that answers from memory, and can be made to fail at any step.</summary>
internal sealed class FakeAppUpdatesPorts(string? newest) : AppUpdates.IPorts
{
    public bool IsInstalledCopy { get; init; } = true;
    public bool FailsToFind { get; init; }
    public bool FailsToDownload { get; init; }
    public bool FailsToApply { get; init; }
    public int Finds { get; private set; }
    public UpdateInfo? Downloaded { get; private set; }
    public UpdateInfo? Applied { get; private set; }

    public bool IsInstalled()
    {
        return IsInstalledCopy;
    }

    public Task<Outcome<UpdateInfo?>> LookUpUpdateAsync(CancellationToken ct)
    {
        Finds++;
        if (FailsToFind)
        {
            return Task.FromResult<Outcome<UpdateInfo?>>(new Outcome<UpdateInfo?>.Failed(new HttpRequestException("Offline")));
        }

        if (newest is null)
        {
            return Task.FromResult<Outcome<UpdateInfo?>>(new Outcome<UpdateInfo?>.Ok(null));
        }

        return Task.FromResult<Outcome<UpdateInfo?>>(new Outcome<UpdateInfo?>.Ok(new UpdateInfo(new VelopackAsset { Version = SemanticVersion.Parse(newest) }, false)));
    }

    public Task<bool> TryDownloadAsync(UpdateInfo update, CancellationToken ct)
    {
        if (FailsToDownload)
        {
            return Task.FromResult(false);
        }

        Downloaded = update;
        return Task.FromResult(true);
    }

    public Task<bool> TryApplyAfterExitAsync(UpdateInfo update, CancellationToken ct)
    {
        if (FailsToApply)
        {
            return Task.FromResult(false);
        }

        Applied = update;
        return Task.FromResult(true);
    }
}

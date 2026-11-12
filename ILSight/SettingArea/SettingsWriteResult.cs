using System.Diagnostics;

namespace ILSight.SettingArea;

/// <summary>What writing the settings file did. A <c>Reason</c> names the file and says why, as the
/// system said it.</summary>
public abstract record SettingsWriteResult
{
    private SettingsWriteResult()
    {
    }

    /// <summary>The file was written, or the settings are kept in memory only and there is none.</summary>
    public sealed record Written : SettingsWriteResult;

    public sealed record Failed(string Reason) : SettingsWriteResult;

    public TResult Match<TResult>(Func<Written, TResult> written, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Written w => written(w),
            Failed f => failed(f),
            _ => throw new UnreachableException($"SettingsWriteResult.Match does not handle {GetType().Name}."),
        };
    }
}

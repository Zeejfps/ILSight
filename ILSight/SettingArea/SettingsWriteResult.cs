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

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Written, TResult> written,
        Func<TState, Failed, TResult> failed)
    {
        return this switch
        {
            Written c => written(state, c),
            Failed c => failed(state, c),
            _ => throw new UnreachableException($"SettingsWriteResult.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Does what the handler of whichever case this is does with it.</summary>
    public void Switch(Action<Written> written, Action<Failed> failed)
    {
        switch (this)
        {
            case Written c:
                written(c);
                return;
            case Failed c:
                failed(c);
                return;
            default:
                throw new UnreachableException($"SettingsWriteResult.Switch does not handle {GetType().Name}.");
        }
    }
}

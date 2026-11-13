using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>Reads the user's NuGet.Config the first time the "open from NuGet" dialog opens, unless it is read or
/// being read, and decides what it means: none is NuGet's default (nuget.org); one that can't be read means nuget.org
/// too, the dialog saying why (ILSight's policy, decided here once); one read gives its sources, none when it lists
/// none. A read that fails unexpectedly is reported, and nuget.org searched in its place. With no feed kept, its first
/// feed is the one searched. Reaches the config and the user's error reports through <see cref="IPorts"/>.</summary>
public sealed class ReadNuGetSetupHandler(ReadNuGetSetupHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user reading the config failed.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Reads the user's NuGet.Config, without holding up the screen: missing, unreadable, or what it says;
        /// a read that fails unexpectedly comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<NuGetConfigRead>> ReadUserConfigAsync(CancellationToken ct);

        /// <summary>The setup of NuGet's default: nuget.org and the default folders.</summary>
        NuGetSetup CreateDefaultSetup();

        /// <summary>The setup <paramref name="config"/> describes.</summary>
        NuGetSetup CreateSetup(NuGetConfig config);
    }

    public async Task HandleAsync(NuGetPicker picker, CancellationToken ct)
    {
        if (!picker.Browser.TryBeginReadingSetup())
        {
            return;
        }

        var read = await ports.ReadUserConfigAsync(ct);
        picker.UseSetup(read.Match(static ok => ok.Value, ReplaceUnreadableConfig).Match(
            _ => ports.CreateDefaultSetup(),
            unreadable => ports.CreateDefaultSetup() with { ConfigUnreadable = unreadable.Why },
            config => ports.CreateSetup(config.Config)));
    }

    private NuGetConfigRead ReplaceUnreadableConfig(Outcome<NuGetConfigRead>.Failed failed)
    {
        ports.ReportError(new AppProblem.NuGetConfigUnreadable(failed.Error));
        return new NuGetConfigRead.Missing();
    }
}

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
        var config = read switch
        {
            Outcome<NuGetConfigRead>.Ok ok => ok.Value,
            Outcome<NuGetConfigRead>.Failed failed => ReplaceUnreadableConfig(failed),
        };
        picker.UseSetup(config switch
        {
            NuGetConfigRead.Missing => ports.CreateDefaultSetup(),
            NuGetConfigRead.Unreadable unreadable => ports.CreateDefaultSetup() with { ConfigUnreadable = unreadable.Why },
            NuGetConfigRead.Read readConfig => ports.CreateSetup(readConfig.Config),
        });
    }

    /// <summary>Has <paramref name="picker"/> search and download with NuGet's default setup when the workflow reading
    /// the user's NuGet.Config failed (the edge's settle, which reports it), as a read that failed does; nothing when a
    /// setup was handed over meanwhile.</summary>
    public void UseDefaultSetup(NuGetPicker picker)
    {
        if (!picker.Browser.IsReadingSetup)
        {
            picker.Browser.EndReadingAgain();
            return;
        }

        picker.UseSetup(ports.CreateDefaultSetup());
    }

    private NuGetConfigRead ReplaceUnreadableConfig(Outcome<NuGetConfigRead>.Failed failed)
    {
        ports.ReportError(new AppProblem.NuGetConfigUnreadable(failed.Error));
        return new NuGetConfigRead.Missing();
    }
}

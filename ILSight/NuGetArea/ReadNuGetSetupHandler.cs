using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>Reads the user's NuGet.Config the first time the "open from NuGet" dialog opens, unless it is read or
/// being read: one that cannot be read at all is reported, and nuget.org searched in its place; with no feed kept,
/// its first feed is the one searched. Reaches the config and the user's error reports through
/// <see cref="IPorts"/>.</summary>
public sealed class ReadNuGetSetupHandler(ReadNuGetSetupHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user reading the config failed.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Reads the setup the user's NuGet.Config describes, without holding up the screen; a config
        /// that cannot be read at all comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<NuGetSetup>> ReadUserSetupAsync(CancellationToken ct);

        /// <summary>The setup to use when the user's config cannot be read: nuget.org and the default folders.</summary>
        NuGetSetup CreateDefaultSetup();
    }

    public async Task HandleAsync(NuGetPicker picker, CancellationToken ct)
    {
        if (!picker.Browser.TryBeginReadingSetup())
        {
            return;
        }

        var read = await ports.ReadUserSetupAsync(ct);
        picker.UseSetup(read.Match(static ok => ok.Value, ReplaceUnreadableSetup));
    }

    private NuGetSetup ReplaceUnreadableSetup(Outcome<NuGetSetup>.Failed failed)
    {
        ports.ReportError(new AppProblem.NuGetConfigUnreadable(failed.Error));
        return ports.CreateDefaultSetup();
    }
}

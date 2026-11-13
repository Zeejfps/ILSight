using ILSight.Core;

namespace ILSight;

/// <summary>Describes references in the open assemblies off the UI thread, for <see cref="LookUpTooltipHandler"/>:
/// on the thread pool, coming back on the UI thread with what the library failed with as <see cref="Outcome{T}.Failed"/>,
/// as the core answers it; the cancellation of the token the call was given is let through.</summary>
public sealed class LookUpTooltipHandlerAdapter(AssemblyList assemblies) : LookUpTooltipHandler.IPorts
{
    public async Task<Outcome<Lookup<CodeTooltip>>> DescribeReferenceAsync(CodeReference.Entity reference, CancellationToken ct)
    {
        var described = await Background.Wait(Task.Run(() => AssemblyListing.DescribeReferenceAsync(assemblies, reference, ct), ct));
        ct.ThrowIfCancellationRequested();
        return described;
    }
}

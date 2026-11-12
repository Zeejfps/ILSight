using ILSight.Core;

namespace ILSight;

/// <summary>Describes references in the open assemblies off the UI thread, for <see cref="LookUpTooltipHandler"/>:
/// on the thread pool, coming back on the UI thread with whatever it threw as <see cref="Outcome{T}.Failed"/>, except
/// the cancellation of the token the call was given, which it lets through.</summary>
public sealed class LookUpTooltipHandlerAdapter(AssemblyList assemblies) : LookUpTooltipHandler.IPorts
{
    public async Task<Outcome<Lookup<CodeTooltip>>> DescribeReferenceAsync(CodeReference.Entity reference, CancellationToken ct)
    {
        var described = await Background.Wait(Task.Run(() => AssemblyListing.DescribeReferenceAsync(assemblies, reference, ct), ct));
        ct.ThrowIfCancellationRequested();
        return described;
    }
}

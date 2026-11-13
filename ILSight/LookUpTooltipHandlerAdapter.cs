using ILSight.Core;

namespace ILSight;

/// <summary>Describes references in the open assemblies off the UI thread, for <see cref="LookUpTooltipHandler"/>:
/// on the thread pool, coming back on the UI thread with what the read failed with (<see cref="UntrustedReads"/>) as <see cref="Outcome{T}.Failed"/>, except
/// the cancellation of the token the call was given, which it lets through.</summary>
public sealed class LookUpTooltipHandlerAdapter(AssemblyList assemblies) : LookUpTooltipHandler.IPorts
{
    public Task<Outcome<Lookup<CodeTooltip>>> DescribeReferenceAsync(CodeReference.Entity reference, CancellationToken ct)
    {
        return UntrustedReads.CatchAsync(Background.Wait(Task.Run(() => AssemblyListing.DescribeReferenceAsync(assemblies, reference, ct), ct)), ct);
    }
}

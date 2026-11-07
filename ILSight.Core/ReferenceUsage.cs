using System.Reflection.Metadata;

namespace ILSight.Core;

/// <summary>What an assembly uses of the assemblies it references: the types it names in each
/// reference and those nested in them, the members of each type it uses, also through the type's
/// generic instances (<c>List&lt;string&gt;.Add</c> counts for <c>List&lt;T&gt;</c>), and the types it
/// forwards to each reference.</summary>
internal sealed class ReferenceUsage
{
    public readonly Dictionary<EntityHandle, List<TypeReferenceHandle>> Types = new();
    public readonly Dictionary<EntityHandle, List<MemberReferenceHandle>> UsedMembers = new();
    public readonly Dictionary<EntityHandle, List<ExportedTypeHandle>> Forwarders = new();
}

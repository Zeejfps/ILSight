namespace ILSight.Core;

/// <summary>What a search looks for; any combination.</summary>
[Flags]
public enum SearchKinds
{
    None = 0,
    Types = 1 << 0,
    Methods = 1 << 1,
    Fields = 1 << 2,
    Properties = 1 << 3,
    Events = 1 << 4,
    /// <summary>Constants, and string and number literals in method bodies.</summary>
    Literals = 1 << 5,
    /// <summary>The definition with a metadata token, in hex.</summary>
    Tokens = 1 << 6,
    Resources = 1 << 7,
    Assemblies = 1 << 8,
    Namespaces = 1 << 9,
    Members = Methods | Fields | Properties | Events,
    TypesAndMembers = Types | Members,
    All = TypesAndMembers | Literals | Tokens | Resources | Assemblies | Namespaces,
}

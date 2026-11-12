namespace ILSight.Core.Comparison;

/// <summary>Which types and members a comparison takes in; never those the compiler made up.</summary>
public enum ApiScope
{
    /// <summary>Only what other assemblies can use.</summary>
    PublicApi,

    /// <summary>Everything the source declared, public or not.</summary>
    AllDeclared,
}

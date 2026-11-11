using System.Diagnostics;
using ILSight.Core.Analysis;
using ILSight.Localization;

namespace ILSight;

/// <summary>The analyzer's words in the interface language.</summary>
internal static class AnalyzerText
{
    public static string Header(Strings t, AnalysisKind kind)
    {
        return kind.Name switch
        {
            AnalysisName.AppliedTo => t.AnalyzerAppliedTo,
            AnalysisName.ImplementedBy => t.AnalyzerImplementedBy,
            AnalysisName.OverriddenBy => t.AnalyzerOverriddenBy,
            AnalysisName.ReadBy => t.AnalyzerReadBy,
            AnalysisName.AssignedBy => t.AnalyzerAssignedBy,
            AnalysisName.AddressTakenBy => t.AnalyzerAddressTakenBy,
            AnalysisName.Implements => t.AnalyzerImplements,
            AnalysisName.Overrides => t.AnalyzerOverrides,
            AnalysisName.UsedBy => t.AnalyzerUsedBy,
            AnalysisName.Uses => t.AnalyzerUses,
            AnalysisName.ExposedBy => t.AnalyzerExposedBy,
            AnalysisName.ExtensionMethods => t.AnalyzerExtensionMethods,
            AnalysisName.InstantiatedBy => t.AnalyzerInstantiatedBy,
            _ => throw new UnreachableException(),
        };
    }
}

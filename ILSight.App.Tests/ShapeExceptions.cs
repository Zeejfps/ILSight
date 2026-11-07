namespace ILSight.App.Tests;

/// <summary>Everything that doesn't fit docs/coding_rules.md, which <see cref="ArchitectureTests"/>
/// holds to exactly.</summary>
internal static class ShapeExceptions
{
    /// <summary>Behaviour an external type demands, by what demands it. These stay.</summary>
    public static readonly Dictionary<string, string> Seams = new()
    {
        ["ILSight.BackgroundWork`1"] = "the compiler's await pattern",
        ["ILSight.BackgroundWork`1+Awaiter"] = "the compiler's await pattern",
        ["ILSight.CodeSelection"] = "ZGF TextSelection, a base class",
        ["ILSight.Core.Analysis.MethodUsesAnalyzer+TypeDefinitionCollector"] = "Decompiler TypeVisitor, a base class",
        ["ILSight.Core.Analysis.TypeExposedByAnalyzer+TypeDefinitionUsedVisitor"] = "Decompiler TypeVisitor, a base class",
        ["ILSight.Core.BamlProjectDecompiler"] = "Decompiler WholeProjectDecompiler, a base class",
        ["ILSight.Core.Documentation+ParameterStarts"] = "Decompiler DecoratingTokenWriter, a base class",
        ["ILSight.Core.HighlightingTokenWriter"] = "Decompiler DecoratingTokenWriter, a base class",
        ["ILSight.Core.MethodBodyWithCSharp"] = "Decompiler MethodBodyDisassembler, a base class",
        ["ILSight.Core.Analysis.FindTypeInAttributeDecoder"] = "Decompiler ICustomAttributeTypeProvider, an interface",
        ["ILSight.Core.AssemblyExport+PartProgress"] = "BCL IProgress, handed to the decompiler",
        ["ILSight.Core.AssemblyResolver"] = "Decompiler IAssemblyResolver, an interface",
        ["ILSight.Core.DebugSymbols"] = "Decompiler IDebugInfoProvider, an interface",
        ["ILSight.Core.DocumentBuilder"] = "Decompiler ITextOutput, an interface",
        ["ILSight.Core.MemberSelection+Constructor"] = "Decompiler IAstTransform, an interface",
        ["ILSight.Core.MemberSelection+Field"] = "Decompiler IAstTransform, an interface",
        ["ILSight.RunArgumentsReceiver"] = "BCL IDisposable, as the app stops it",
        ["ILSight.Core.TextListConverter"] = "System.Text.Json JsonConverter, a base class",
        ["ILSight.CodeViewPalette"] = "ZGF ThemeColorsGenerator generates its color tables, with no GeneratedCode to tell by",
        ["ILSight.ILSightApp"] = "ZGF ImmediateGuiApp, the base class of an app",
        ["ILSight.Localization.LocalizationService"] = "ZGF LocalizationGenerator generates it, with no GeneratedCode to tell by",
        ["ILSight.Localization.Strings"] = "ZGF LocalizationGenerator generates it as a class",
        ["ILSight.NodeIconPalette"] = "ZGF ThemeColorsGenerator generates its color tables, with no GeneratedCode to tell by",
    };

    /// <summary>Methods on records an external type demands, by what demands them. These stay.</summary>
    public static readonly Dictionary<string, string> RecordSeams = new()
    {
        ["ILSight.TextPosition.CompareTo"] = "UiSelectableText's constraint on its position type",
    };

    /// <summary>Types of our own that are entirely self-contained: they depend on nothing but each other,
    /// change nothing outside themselves, and could be used anywhere. These stay, with their methods.</summary>
    public static readonly string[] SelfContained =
    [
        "ILSight.CameraZoom",
        "ILSight.CodeFontSize",
        "ILSight.Core.AssemblyPath",
        "ILSight.Core.AssemblyPicks`1",
        "ILSight.Core.NaturalStringComparer",
        "ILSight.Core.NuGetSource",
        "ILSight.Core.PackageId",
        "ILSight.Core.PackageVersion",
        "ILSight.Core.TargetFrameworkId",
        "ILSight.Core.WebUrl",
        "ILSight.PictureZoom",
        "ILSight.ThemeId`1",
        "ILSight.UserIntent",
    ];

    /// <summary>Unions rebuilt every frame, so structs rather than objects. These stay.</summary>
    public static readonly string[] StructUnions =
    [
        "ILSight.CodeHover",
        "ILSight.ViewPosition",
    ];

    /// <summary>Types with methods, still to become data and functions.</summary>
    public static readonly string[] NotYetData =
    [
        "ILSight.AnalyzerKindNode",
        "ILSight.AnalyzerNode",
        "ILSight.AnalyzerRows",
        "ILSight.AnalyzerSymbolNode",
        "ILSight.AnalyzerTree",
        "ILSight.AppState",
        "ILSight.BrowseTree",
        "ILSight.CodeFolds",
        "ILSight.CodeViewState",
        "ILSight.ComparisonRows",
        "ILSight.Core.AssemblyList",
        "ILSight.Core.BrowseNode",
        "ILSight.Core.BrowseSession",
        "ILSight.Core.LoadedAssembly",
        "ILSight.Core.LoadedModule",
        "ILSight.Core.LoadedPackage",
        "ILSight.Core.Metadata.GridSchema",
        "ILSight.Core.Metadata.GridSchema`1",
        "ILSight.DiagramViewState",
        "ILSight.DocumentTab",
        "ILSight.DocumentTabs",
        "ILSight.DocumentViews",
        "ILSight.MetadataGridState",
        "ILSight.UiState",
    ];

    /// <summary>Constructors that take arguments, still to become object initializers or functions.</summary>
    public static readonly string[] ConstructorsLeft =
    [
        "ILSight.AnalyzerKindNode",
        "ILSight.AnalyzerMessageNode",
        "ILSight.AnalyzerPendingNode",
        "ILSight.AnalyzerRows",
        "ILSight.AnalyzerSymbolNode",
        "ILSight.AppIcons",
        "ILSight.BrowseTree",
        "ILSight.Core.AssemblyLoadException",
        "ILSight.Core.FailedAssembly",
        "ILSight.Core.LoadedAssembly",
        "ILSight.Core.LoadedModule",
        "ILSight.Core.LoadedPackage",
        "ILSight.Core.LocatingTokenWriter",
        "ILSight.Core.Metadata.GridSchema",
        "ILSight.Core.Metadata.GridSchema`1",
        "ILSight.MetadataGridState",
        "ILSight.UiState",
    ];

    /// <summary>Extension functions, still to become plain ones.</summary>
    public static readonly string[] ExtensionsLeft =
    [
        "ILSight.Localization.LocalizationWidgetExtensions.Localization",
    ];

    /// <summary>Methods on records, still to move to static functions.</summary>
    public static readonly string[] RecordsWithMethods =
    [
        "ILSight.Core.DecompilerOptions.Allows",
        "ILSight.Core.DecompilerOptions.Apply",
        "ILSight.Core.DecompilerOptions.HasSameToggles",
        "ILSight.Core.DecompilerOptions.IsChanged",
        "ILSight.Core.DecompilerOptions.IsOn",
        "ILSight.Core.DecompilerOptions.ProducesSameCode",
        "ILSight.Core.DecompilerOptions.With",
        "ILSight.Core.DecompilerOptions.WithCodeTogglesReset",
        "ILSight.Core.DecompilerOptions.WithoutProjectExport",
    ];

    /// <summary>Static caches that are purely a speed-up, by why they may stay, and the one seam. These stay.</summary>
    public static readonly Dictionary<string, string> AllowedStatics = new()
    {
        ["ILSight.BookmarksPanelView.HeaderTexts"] = "text per language",
        ["ILSight.DisplayOptionsPanelView.TokenFormatLabels"] = "text per language",
        ["ILSight.DisplayOptionsPanelView.UiScaleLabels"] = "text per language",
        ["ILSight.SearchPanelView.Headers"] = "text per language",
        ["ILSight.SearchPanelView.Summaries"] = "text per language",
        ["ILSight.ToolbarView.LanguageSegments"] = "text per language",
        ["ILSight.NodeText.AssemblyLabels"] = "text per assembly, gone with it",
        ["ILSight.NodeText.DecimalTokens"] = "text per node, gone with it",
        ["ILSight.NodeText.HexTokens"] = "text per node, gone with it",
        ["ILSight.NodeText.Secondaries"] = "text per node, gone with it",
        ["ILSight.NodeText.Tooltips"] = "text per node, gone with it",
        ["ILSight.Core.Metadata.FlagsSchemaInferer.cache"] = "worked out per enum type, of which there are few",
        ["ILSight.Core.Metadata.MetadataGrid.Patterns"] = "worked out per pattern, up to MaxPatterns",
        ["ILSight.Core.NuGetFeed.Shared"] = "a seam: one HttpClient is how it is meant to be used",
    };
}

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
        ["ILSight.Core.CodeTooltip+ParameterStarts"] = "Decompiler DecoratingTokenWriter, a base class",
        ["ILSight.Core.HighlightingTokenWriter"] = "Decompiler DecoratingTokenWriter, a base class",
        ["ILSight.Core.MethodBodyWithCSharp"] = "Decompiler MethodBodyDisassembler, a base class",
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
        "ILSight.AppSettings",
        "ILSight.AppState",
        "ILSight.AssemblyComparison",
        "ILSight.AssemblyListsDialog",
        "ILSight.Bookmark",
        "ILSight.BrowseTree",
        "ILSight.CodeFolds",
        "ILSight.CodeFontChoices",
        "ILSight.CodeFontFaces",
        "ILSight.CodeTooltips",
        "ILSight.CodeViewState",
        "ILSight.ComparisonRows",
        "ILSight.Core.Analysis.AnalysisSymbol",
        "ILSight.Core.Analysis.AnalyzerContext",
        "ILSight.Core.Analysis.AnalyzerScope",
        "ILSight.Core.Analysis.FindTypeInAttributeDecoder",
        "ILSight.Core.AssemblyExport+PartProgress",
        "ILSight.Core.AssemblyList",
        "ILSight.Core.AssemblyPackage",
        "ILSight.Core.AssemblyPackage+FileTree",
        "ILSight.Core.AssemblyResolver",
        "ILSight.Core.AssemblySearch+Search",
        "ILSight.Core.BrowseNode",
        "ILSight.Core.BrowseSession",
        "ILSight.Core.CodeReference+Assembly",
        "ILSight.Core.CodeReference+Entity",
        "ILSight.Core.CodeReference+Label",
        "ILSight.Core.CodeReference+Local",
        "ILSight.Core.Comparison.ApiDiff",
        "ILSight.Core.Comparison.ApiEntry+Entity",
        "ILSight.Core.Comparison.ApiEntry+Namespace",
        "ILSight.Core.DebugSymbols",
        "ILSight.Core.DecompiledDocument",
        "ILSight.Core.DecompilerToggle",
        "ILSight.Core.Diagrams.ClassDiagrams+BuiltTypes",
        "ILSight.Core.Diagrams.DiagramDocs",
        "ILSight.Core.Diagrams.DiagramNames",
        "ILSight.Core.Diagrams.DiagramTypes",
        "ILSight.Core.Diagrams.MermaidMembers",
        "ILSight.Core.Diagrams.TypeLayout",
        "ILSight.Core.DocText",
        "ILSight.Core.DocumentBuilder",
        "ILSight.Core.DocumentLine",
        "ILSight.Core.LoadedAssembly",
        "ILSight.Core.LoadedModule",
        "ILSight.Core.LoadedPackage",
        "ILSight.Core.MemberSelection+Constructor",
        "ILSight.Core.MemberSelection+Field",
        "ILSight.Core.Metadata.FilterState",
        "ILSight.Core.Metadata.GridSchema",
        "ILSight.Core.Metadata.GridSchema`1",
        "ILSight.Core.Metadata.MetadataFolderInfo",
        "ILSight.Core.Metadata.MetadataGrid",
        "ILSight.Core.Metadata.MetadataTableInfo",
        "ILSight.Core.NodePath",
        "ILSight.Core.NuGetFeed",
        "ILSight.Core.NuGetSetup",
        "ILSight.Core.PackageCache",
        "ILSight.Core.PackageFileInfo",
        "ILSight.Core.PackageFolderInfo",
        "ILSight.Core.PackageIcons",
        "ILSight.Core.ReferenceUsage",
        "ILSight.Core.Resources.ResourceContainerInfo",
        "ILSight.Core.Resources.ResourceEntryInfo",
        "ILSight.Core.Resources.ResourceImage",
        "ILSight.Core.SearchQuery",
        "ILSight.DiagramViewState",
        "ILSight.DocumentTab",
        "ILSight.DocumentTabs",
        "ILSight.DocumentViews",
        "ILSight.ExportQueue",
        "ILSight.ExportSetup",
        "ILSight.IconThemeCatalog",
        "ILSight.IconThemeFile",
        "ILSight.ItemSelection`1",
        "ILSight.LayoutTransition",
        "ILSight.MainLayout",
        "ILSight.MergedIconTheme",
        "ILSight.MergedTheme",
        "ILSight.MetadataGridState",
        "ILSight.NuGetPicker",
        "ILSight.RunArgumentsReceiver",
        "ILSight.SavedAssemblyLists",
        "ILSight.ThemeCatalog",
        "ILSight.ThemeCatalog`3",
        "ILSight.ThemeFile",
        "ILSight.ThemeFolderWatch",
        "ILSight.ThemeJson",
        "ILSight.TypeDiagram",
        "ILSight.UiState",
    ];

    /// <summary>Methods on records, still to move to static functions.</summary>
    public static readonly string[] RecordsWithMethods =
    [
        "ILSight.Core.Activation.Of",
        "ILSight.Core.Activation.TargetOf",
        "ILSight.Core.CodeTooltip.ConstraintLines",
        "ILSight.Core.CodeTooltip.ContainerOf",
        "ILSight.Core.CodeTooltip.Create",
        "ILSight.Core.CodeTooltip.KeywordOf",
        "ILSight.Core.CodeTooltip.WriteConstraints",
        "ILSight.Core.CodeTooltip.WriteSignature",
        "ILSight.Core.DecompilerOptions.Allows",
        "ILSight.Core.DecompilerOptions.Apply",
        "ILSight.Core.DecompilerOptions.HasSameToggles",
        "ILSight.Core.DecompilerOptions.IsChanged",
        "ILSight.Core.DecompilerOptions.IsOn",
        "ILSight.Core.DecompilerOptions.ProducesSameCode",
        "ILSight.Core.DecompilerOptions.With",
        "ILSight.Core.DecompilerOptions.WithCodeTogglesReset",
        "ILSight.Core.DecompilerOptions.WithoutProjectExport",
        "ILSight.Core.Diagrams.RoutePiece.Blend",
        "ILSight.Core.Diagrams.RoutePiece.Lerp",
        "ILSight.Core.Diagrams.RoutePiece.Line",
        "ILSight.Core.Diagrams.RoutePiece.Split",
        "ILSight.Core.NodePathSegment.Of",
        "ILSight.Core.NodePathSegment.UsedMemberId",
        "ILSight.Core.NodeVisibility.Of",
        "ILSight.Core.NodeVisibility.OfDefinition",
        "ILSight.Core.NodeVisibility.OfReferenced",
        "ILSight.Core.NodeVisibility.OfResource",
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

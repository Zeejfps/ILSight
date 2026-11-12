using System.Diagnostics;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight;

/// <summary>Each node kind's icon, drawn as the icon theme draws its <c>node.*</c> key, and the role
/// that colors it.</summary>
public static class NodeIcons
{
    /// <summary>The icon of a node of <paramref name="subject"/>.</summary>
    public static NodeIcon Of(NodeSubject subject)
    {
        return subject.Match(
            module: static _ => NodeIcon.Assembly,
            @namespace: static _ => NodeIcon.Namespace,
            failedAssembly: static _ => NodeIcon.FailedAssembly,
            typeDefinition: static type => Of(type.Definition.Shape),
            memberDefinition: static member => Of(member.Definition.Shape),
            references: static _ => NodeIcon.ReferencesFolder,
            assemblyReference: static _ => NodeIcon.AssemblyReference,
            referencedTypes: static _ => NodeIcon.ReferencedTypes,
            typeReference: static _ => NodeIcon.TypeReference,
            memberReference: static member => member.Value.Shape switch
            {
                MemberShape.Field or MemberShape.Constant => NodeIcon.FieldReference,
                MemberShape.Property or MemberShape.Indexer => NodeIcon.PropertyReference,
                MemberShape.Event => NodeIcon.EventReference,
                MemberShape.Method or MemberShape.Constructor or MemberShape.Operator => NodeIcon.MethodReference,
                _ => throw new UnreachableException(),
            },
            moduleReference: static _ => NodeIcon.ModuleReference,
            importedMethod: static _ => NodeIcon.ImportedMethod,
            forwardedType: static _ => NodeIcon.ForwardedType,
            baseTypes: static _ => NodeIcon.BaseTypes,
            baseType: static _ => NodeIcon.BaseType,
            derivedTypes: static _ => NodeIcon.DerivedTypes,
            derivedType: static _ => NodeIcon.DerivedType,
            unreadableModule: static _ => NodeIcon.FailedAssembly,
            resources: static _ => NodeIcon.ResourcesFolder,
            resourceContainer: static _ => NodeIcon.ResourcesFile,
            resourceEntry: static entry => entry.Value.IsImage ? NodeIcon.ResourceImage : NodeIcon.Resource,
            metadata: static _ => NodeIcon.Metadata,
            metadataTables: static _ => NodeIcon.MetadataTables,
            metadataTable: static table => table.Value.Role switch
            {
                MetadataGridRole.Header => NodeIcon.MetadataHeader,
                MetadataGridRole.Directory => NodeIcon.MetadataDirectory,
                MetadataGridRole.Table => NodeIcon.MetadataTable,
                MetadataGridRole.Heap => NodeIcon.MetadataHeap,
                _ => throw new UnreachableException(),
            },
            package: static package => !package.Value.IsRoot ? NodeIcon.PackageFolder
                : package.Value.Package.Package.IsBundle ? NodeIcon.Bundle : NodeIcon.Package,
            packageFile: static file => file.Value.IsImage ? NodeIcon.ResourceImage : NodeIcon.Resource);
    }

    /// <summary>The icon of a symbol of <paramref name="shape"/>, as an analysis, a comparison or a diagram lists it.</summary>
    public static NodeIcon Of(SymbolShape shape)
    {
        return shape.Match(
            static _ => NodeIcon.Assembly,
            static _ => NodeIcon.Namespace,
            static type => Of(type.Shape),
            static member => Of(member.Shape));
    }

    public static NodeIcon Of(TypeShape shape)
    {
        return shape switch
        {
            TypeShape.Class => NodeIcon.Class,
            TypeShape.Interface => NodeIcon.Interface,
            TypeShape.Struct => NodeIcon.Struct,
            TypeShape.Enum => NodeIcon.Enum,
            TypeShape.Delegate => NodeIcon.Delegate,
            TypeShape.Record => NodeIcon.Record,
            TypeShape.RecordStruct => NodeIcon.RecordStruct,
            _ => throw new UnreachableException(),
        };
    }

    public static NodeIcon Of(MemberShape shape)
    {
        return shape switch
        {
            MemberShape.Field => NodeIcon.Field,
            MemberShape.Constant => NodeIcon.Constant,
            MemberShape.Property => NodeIcon.Property,
            MemberShape.Indexer => NodeIcon.Indexer,
            MemberShape.Event => NodeIcon.Event,
            MemberShape.Method => NodeIcon.Method,
            MemberShape.Constructor => NodeIcon.Constructor,
            MemberShape.Operator => NodeIcon.Operator,
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>The icon of <paramref name="node"/> as the tree shows it, with the overlay of its accessibility.</summary>
    public static UiIcon Icon(BrowseNode node, NodeIconPalette palette, AppIcons icons)
    {
        return Icon(Of(node.Subject), node.Visibility.Accessibility, palette, icons);
    }

    /// <summary>Records <paramref name="node"/>'s <see cref="Icon(BrowseNode, NodeIconPalette, AppIcons)"/>, in the
    /// screen's icons and icon colors.</summary>
    public static void Draw(Ui<UiState> ui, BrowseNode node)
    {
        ui.Icon(Icon(node, ui.State.NodeIconPalette, ui.State.Icons));
    }

    /// <summary>What a node of <paramref name="kind"/> draws in <paramref name="icons"/>.</summary>
    public static IconGlyph Glyph(NodeIcon kind, AppIcons icons)
    {
        return kind switch
        {
            NodeIcon.Assembly => icons.NodeAssembly,
            NodeIcon.ReferencesFolder => icons.NodeReferencesFolder,
            NodeIcon.AssemblyReference => icons.NodeAssemblyReference,
            NodeIcon.ResourcesFolder => icons.NodeResourcesFolder,
            NodeIcon.Resource => icons.NodeResource,
            NodeIcon.Namespace => icons.NodeNamespace,
            NodeIcon.Class => icons.NodeClass,
            NodeIcon.Interface => icons.NodeInterface,
            NodeIcon.Struct => icons.NodeStruct,
            NodeIcon.Enum => icons.NodeEnum,
            NodeIcon.Delegate => icons.NodeDelegate,
            NodeIcon.Record => icons.NodeRecord,
            NodeIcon.RecordStruct => icons.NodeRecordStruct,
            NodeIcon.BaseTypes => icons.NodeBaseTypes,
            NodeIcon.BaseType => icons.NodeBaseType,
            NodeIcon.Field => icons.NodeField,
            NodeIcon.Constant => icons.NodeConstant,
            NodeIcon.Property => icons.NodeProperty,
            NodeIcon.Indexer => icons.NodeIndexer,
            NodeIcon.Event => icons.NodeEvent,
            NodeIcon.Method => icons.NodeMethod,
            NodeIcon.Constructor => icons.NodeConstructor,
            NodeIcon.Operator => icons.NodeOperator,
            NodeIcon.Metadata => icons.NodeMetadata,
            NodeIcon.MetadataHeader => icons.NodeMetadataHeader,
            NodeIcon.MetadataDirectory => icons.NodeMetadataDirectory,
            NodeIcon.MetadataTables => icons.NodeMetadataTables,
            NodeIcon.MetadataTable => icons.NodeMetadataTable,
            NodeIcon.MetadataHeap => icons.NodeMetadataHeap,
            NodeIcon.DerivedTypes => icons.NodeDerivedTypes,
            NodeIcon.DerivedType => icons.NodeDerivedType,
            NodeIcon.ResourcesFile => icons.NodeResourcesFile,
            NodeIcon.ResourceImage => icons.NodeResourceImage,
            NodeIcon.FailedAssembly => icons.NodeFailedAssembly,
            NodeIcon.ForwardedType => icons.NodeForwardedType,
            NodeIcon.Package => icons.NodePackage,
            NodeIcon.PackageFolder => icons.NodePackageFolder,
            NodeIcon.Bundle => icons.NodeBundle,
            NodeIcon.ReferencedTypes => icons.NodeReferencedTypes,
            NodeIcon.TypeReference => icons.NodeTypeReference,
            NodeIcon.MethodReference => icons.NodeMethodReference,
            NodeIcon.FieldReference => icons.NodeFieldReference,
            NodeIcon.PropertyReference => icons.NodePropertyReference,
            NodeIcon.EventReference => icons.NodeEventReference,
            NodeIcon.ModuleReference => icons.NodeModuleReference,
            NodeIcon.ImportedMethod => icons.NodeImportedMethod,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static NodeIconRole Role(NodeIcon kind)
    {
        return kind switch
        {
            NodeIcon.Assembly => NodeIconRole.Neutral,
            NodeIcon.ReferencesFolder => NodeIconRole.Neutral,
            NodeIcon.AssemblyReference => NodeIconRole.Neutral,
            NodeIcon.ResourcesFolder => NodeIconRole.Neutral,
            NodeIcon.Resource => NodeIconRole.Neutral,
            NodeIcon.Namespace => NodeIconRole.Neutral,
            NodeIcon.Class => NodeIconRole.Type,
            NodeIcon.Interface => NodeIconRole.Type,
            NodeIcon.Struct => NodeIconRole.Type,
            NodeIcon.Enum => NodeIconRole.Type,
            NodeIcon.Delegate => NodeIconRole.Type,
            NodeIcon.Record => NodeIconRole.Type,
            NodeIcon.RecordStruct => NodeIconRole.Type,
            NodeIcon.BaseTypes => NodeIconRole.Neutral,
            NodeIcon.BaseType => NodeIconRole.Type,
            NodeIcon.Field => NodeIconRole.Data,
            NodeIcon.Constant => NodeIconRole.Data,
            NodeIcon.Property => NodeIconRole.Data,
            NodeIcon.Indexer => NodeIconRole.Data,
            NodeIcon.Event => NodeIconRole.Data,
            NodeIcon.Method => NodeIconRole.Method,
            NodeIcon.Constructor => NodeIconRole.Method,
            NodeIcon.Operator => NodeIconRole.Method,
            NodeIcon.Metadata => NodeIconRole.Neutral,
            NodeIcon.MetadataHeader => NodeIconRole.Neutral,
            NodeIcon.MetadataDirectory => NodeIconRole.Neutral,
            NodeIcon.MetadataTables => NodeIconRole.Neutral,
            NodeIcon.MetadataTable => NodeIconRole.Data,
            NodeIcon.MetadataHeap => NodeIconRole.Data,
            NodeIcon.DerivedTypes => NodeIconRole.Neutral,
            NodeIcon.DerivedType => NodeIconRole.Type,
            NodeIcon.ResourcesFile => NodeIconRole.Neutral,
            NodeIcon.ResourceImage => NodeIconRole.Neutral,
            NodeIcon.FailedAssembly => NodeIconRole.Error,
            NodeIcon.ForwardedType => NodeIconRole.Type,
            NodeIcon.Package => NodeIconRole.Neutral,
            NodeIcon.PackageFolder => NodeIconRole.Neutral,
            NodeIcon.Bundle => NodeIconRole.Neutral,
            NodeIcon.ReferencedTypes => NodeIconRole.Neutral,
            NodeIcon.TypeReference => NodeIconRole.Type,
            NodeIcon.MethodReference => NodeIconRole.Method,
            NodeIcon.FieldReference => NodeIconRole.Data,
            NodeIcon.PropertyReference => NodeIconRole.Data,
            NodeIcon.EventReference => NodeIconRole.Data,
            NodeIcon.ModuleReference => NodeIconRole.Neutral,
            NodeIcon.ImportedMethod => NodeIconRole.Method,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>The small pictogram over a node's icon that marks it as not visible outside its
    /// assembly or type: a shield for internal, a lock for private, as <paramref name="icons"/> draw
    /// them. Null for anything wider.</summary>
    public static IconGlyph? Overlay(NodeAccessibility accessibility, AppIcons icons)
    {
        return accessibility switch
        {
            NodeAccessibility.Internal => icons.OverlayInternal,
            NodeAccessibility.Private or NodeAccessibility.PrivateProtected => icons.OverlayPrivate,
            _ => null,
        };
    }

    /// <summary>The icon of a node of <paramref name="kind"/> as the tree shows it: drawn as
    /// <paramref name="icons"/> draw it, colored from <paramref name="palette"/>, with the overlay of its
    /// <paramref name="accessibility"/>.</summary>
    public static UiIcon Icon(NodeIcon kind, NodeAccessibility accessibility, NodeIconPalette palette, AppIcons icons)
    {
        return new UiIcon(Glyph(kind, icons), GetColor(palette, kind), Overlay: Overlay(accessibility, icons), OverlayColor: palette.Restricted);
    }

    /// <summary>The color <paramref name="palette"/> draws a node of <paramref name="kind"/> in.</summary>
    public static uint GetColor(NodeIconPalette palette, NodeIcon kind)
    {
        return palette[Role(kind)];
    }

    /// <summary>The icon of the row shown while a node's children are being read.</summary>
    public static UiIcon Loading(NodeIconPalette palette, AppIcons icons)
    {
        return new UiIcon(icons.NodeLoading, palette.Neutral);
    }

    /// <summary>The icon of the row shown in place of children that could not be read.</summary>
    public static UiIcon Failed(NodeIconPalette palette, AppIcons icons)
    {
        return new UiIcon(icons.NodeError, palette.Error);
    }

    /// <summary>Records <paramref name="kind"/>'s <see cref="Icon"/> as the tree shows it, in the screen's
    /// icons and icon colors.</summary>
    public static void Draw(Ui<UiState> ui, NodeIcon kind, NodeAccessibility accessibility = NodeAccessibility.None)
    {
        ui.Icon(Icon(kind, accessibility, ui.State.NodeIconPalette, ui.State.Icons));
    }
}

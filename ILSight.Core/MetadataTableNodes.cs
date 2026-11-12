using System.Reflection.Metadata.Ecma335;
using ILSight.Core.Metadata;

namespace ILSight.Core;

/// <summary>The nodes under a metadata folder's "Tables": a node per table, made from the folder's image alone (how many
/// rows each table has, and the grid that reads them). Calls no file and no type system; for an image read whole into
/// memory, the session makes them where a link needs them (<see cref="BrowseSession.FindMetadataRow"/>), as the tree's
/// build does.</summary>
internal static class MetadataTableNodes
{
    private static readonly Activation.OpenSelf Open = new();

    /// <summary>A node per table: the grid of its rows. An empty table, whose grid cannot always be read,
    /// and a table ILSpy does not list show only how many rows there are.</summary>
    internal static BrowseNode[] List(BrowseNode parent, MetadataFolderInfo folder)
    {
        var file = folder.File;
        var result = new List<BrowseNode>();
        foreach (var index in Enum.GetValues<TableIndex>())
        {
            var rows = file.Metadata.GetTableRowCount(index);
            var schema = rows == 0 ? null : MetadataTables.Create(index, file);
            if (schema is null)
            {
                result.Add(Create(parent, MetadataTables.CreateRowCountPart(folder.Module, file, index, rows)));
            }
            else
            {
                result.Add(Create(parent, MetadataTables.CreateGridPart(folder.Module, file, $"{(byte)index:X2} {index} ({rows})", new MetadataPartKey.Table(index),
                    MetadataGridRole.Table, schema)));
            }
        }
        return result.ToArray();
    }

    /// <summary>The node of <paramref name="part"/> under <paramref name="parent"/>, with the values a node of a
    /// metadata table shows, all read from <paramref name="part"/>.</summary>
    private static BrowseNode Create(BrowseNode parent, MetadataTableInfo part)
    {
        var text = part.Title;
        var segment = new NodePathSegment.MetadataPart(part.Key);
        return new BrowseNode
        {
            Assembly = part.Module,
            Parent = parent,
            Text = text,
            Subject = new NodeSubject.MetadataTable { Value = part },
            EnclosingNamespace = null,
            Segment = segment,
            Path = new NodePath { FilePath = parent.Path.FilePath, Steps = parent.Path.Steps.Add(new NodePathStep(segment, text)) },
            FullName = text,
            Visibility = NodeVisibility.Open,
            Reference = null,
            Activation = Open,
            Analysis = null,
            CanHaveChildren = part.Part is MetadataPart.DebugDirectory,
            Name = text,
            IsCode = part.Part is not (MetadataPart.Grid or MetadataPart.DebugDirectory),
            ResourceFileName = null,
            ILSpyName = null,
            EntityFullName = null,
        };
    }
}

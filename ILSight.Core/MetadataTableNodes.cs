using ICSharpCode.Decompiler.Metadata;
using System.Reflection.Metadata.Ecma335;
using ILSight.Core.MetadataArea;

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
            var rows = ListingReads.ValueOf(ReadRowCount(file, index));
            var schema = rows == 0 ? null : ListingReads.ValueOf(ReadSchema(file, index));
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

    private static Outcome<int> ReadRowCount(MetadataFile file, TableIndex index)
    {
        try
        {
            return new Outcome<int>.Ok(file.Metadata.GetTableRowCount(index));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<int>.Failed(e);
        }
    }

    // The grid of the table's rows; none for a table ILSpy does not list.
    private static Outcome<GridSchema?> ReadSchema(MetadataFile file, TableIndex index)
    {
        try
        {
            return new Outcome<GridSchema?>.Ok(MetadataTables.Create(index, file));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<GridSchema?>.Failed(e);
        }
    }

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
            Path = new NodePath
            {
                FilePath = parent.Path.FilePath,
                Steps = parent.Path.Steps.Add(new NodePathStep
                {
                    Segment = segment,
                    Label = text,
                }),
            },
            FullName = text,
            Visibility = NodeVisibility.Open,
            Reference = null,
            Activation = Open,
            AnalysisFacts = null,
            CanHaveChildren = part.Part is MetadataPart.DebugDirectory,
            Name = text,
            IsCode = part.Part is not (MetadataPart.Grid or MetadataPart.DebugDirectory),
            ResourceFileName = null,
            ILSpyName = null,
            EntityFullName = null,
        };
    }
}

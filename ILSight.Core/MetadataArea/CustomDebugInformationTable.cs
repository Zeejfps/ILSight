using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.MetadataArea;

/// <summary>
/// The CustomDebugInformation table: extensible per-entity payloads used for async/iterator
/// state-machine info, embedded source, source-link JSON, and similar debug-time data. Each row's
/// Kind GUID shows as heap offset, raw GUID and decoded friendly name, and its parsed Value blob
/// as the row's details.
/// </summary>
internal static class CustomDebugInformationTable
{
    private static readonly (Guid Guid, string Name)[] KnownKindNames =
    [
        (KnownGuids.StateMachineHoistedLocalScopes, "State Machine Hoisted Local Scopes (C# / VB)"),
        (KnownGuids.DynamicLocalVariables, "Dynamic Local Variables (C#)"),
        (KnownGuids.DefaultNamespaces, "Default Namespaces (VB)"),
        (KnownGuids.EditAndContinueLocalSlotMap, "Edit And Continue Local Slot Map (C# / VB)"),
        (KnownGuids.EditAndContinueLambdaAndClosureMap, "Edit And Continue Lambda And Closure Map (C# / VB)"),
        (KnownGuids.EncStateMachineStateMap, "Edit And Continue State Machine State Map (C# / VB)"),
        (KnownGuids.EmbeddedSource, "Embedded Source (C# / VB)"),
        (KnownGuids.SourceLink, "Source Link (C# / VB)"),
        (KnownGuids.MethodSteppingInformation, "Method Stepping Information (C# / VB)"),
        (KnownGuids.CompilationOptions, "Compilation Options (C# / VB)"),
        (KnownGuids.CompilationMetadataReferences, "Compilation Metadata References (C# / VB)"),
        (KnownGuids.TupleElementNames, "Tuple Element Names (C#)"),
        (KnownGuids.TypeDefinitionDocuments, "Type Definition Documents (C# / VB)"),
    ];

    public static GridSchema<CustomDebugInformation> Columns(GridSchemaBuilder<CustomDebugInformation> table, MetadataFile file)
    {
        var m = file.Metadata;
        GridSchemas.AddOffset(table, TableIndex.CustomDebugInformation);
        GridSchemas.AddHandle(table, "Parent", r => r.Parent);
        GridSchemas.AddValue(table, "Kind", r => MetadataTokens.GetHeapOffset(r.Kind), "X2");
        GridSchemas.AddValue(table, "KindGUID", r => m.GetGuid(r.Kind), "D");
        GridSchemas.AddValue(table, "KindString", r => KindString(m, r.Kind));
        GridSchemas.AddValue(table, "Info", r => FindInfo(m, r));
        GridSchemas.AddBlob(table, "Value", r => r.Value, r => r.Value.IsNil ? NilValue : m.GetBlobBytes(r.Value), ExplainValue);
        GridSchemas.AddValue(table, "ValueLength", r => m.GetBlobReader(r.Value).Length, "X8");
        GridSchemas.SetDetails(table, r => ReadDetails(m, r), static MetadataRowDetails? (read) => MakeDetails(read));
        return table.ToSchema();
    }

    private const string NilValue = "<nil>";

    // The Value tooltip, made after the cell's read of the blob's bytes: the bytes in hex, "<nil>" for a row with no
    // blob, none for an empty one.
    private static MetadataTooltip? ExplainValue(object read)
    {
        if (read is not byte[] bytes)
        {
            return new MetadataTooltip { Text = NilValue, Groups = [] };
        }

        if (bytes.Length == 0)
        {
            return null;
        }

        return new MetadataTooltip { Text = GridSchemas.FormatBlobBytes(bytes), Groups = [] };
    }

    private static string? FindInfo(MetadataReader metadata, CustomDebugInformation row)
    {
        if (row.Kind.IsNil)
        {
            return null;
        }

        var kind = metadata.GetGuid(row.Kind);
        if (kind != KnownGuids.EmbeddedSource)
        {
            return null;
        }

        return EmbeddedSourceFormat(metadata, row.Value);
    }

    private static string KindString(MetadataReader metadata, GuidHandle kind)
    {
        if (kind.IsNil)
        {
            return "";
        }

        var guid = metadata.GetGuid(kind);
        foreach (var (knownGuid, knownName) in KnownKindNames)
        {
            if (guid == knownGuid)
            {
                return knownName;
            }
        }
        return "Unknown";
    }

    private static string EmbeddedSourceFormat(MetadataReader metadata, BlobHandle value)
    {
        if (value.IsNil)
        {
            return "{nil blob}";
        }

        var reader = metadata.GetBlobReader(value);
        if (reader.RemainingBytes < 4)
        {
            return "{blob too short}";
        }

        var format = reader.ReadInt32();
        return format switch
        {
            < 0 => $"Unknown format '{format}', {reader.Length} bytes",
            0 => $"Raw, {reader.Length} bytes",
            > 0 => $"DEFLATE, {reader.Length} bytes, {format} uncompressed",
        };
    }

    // The read half of a row's details, inside the row's catch: its kind, where its value's blob is, and for embedded
    // source the extension of the document it is; null for a row with no value or kind.
    private static DetailsRead? ReadDetails(MetadataReader metadata, CustomDebugInformation row)
    {
        if (row.Value.IsNil || row.Kind.IsNil)
        {
            return null;
        }

        var kind = metadata.GetGuid(row.Kind);
        return new DetailsRead
        {
            Kind = kind,
            Value = metadata.GetBlobReader(row.Value),
            Extension = kind == KnownGuids.EmbeddedSource ? EmbeddedSourceExtension(metadata, row.Parent) : null,
        };
    }

    // The format half, after the read's catch: the app's own decoding of the value's bytes, whose failure on bad data is
    // the bytes shown in hex; anything else it throws is the app's bug.
    private static MetadataRowDetails MakeDetails(DetailsRead read)
    {
        var reader = read.Value;
        try
        {
            return Parse(read, ref reader);
        }
        catch (BadImageFormatException)
        {
            return new MetadataRowDetails.Text { Content = read.Value.ToHexString(), Extension = null };
        }
        catch (InvalidDataException)
        {
            return new MetadataRowDetails.Text { Content = read.Value.ToHexString(), Extension = null };
        }
    }

    private static MetadataRowDetails Parse(DetailsRead read, ref BlobReader reader)
    {
        var kind = read.Kind;
        if (kind == KnownGuids.StateMachineHoistedLocalScopes)
        {
            var rows = new List<IReadOnlyList<string>>();
            while (reader.RemainingBytes > 0)
            {
                var first = reader.ReadUInt32();
                var second = reader.ReadUInt32();
                rows.Add([first.ToString(), second.ToString()]);
            }

            return new MetadataRowDetails.Table { Headers = ["Start Offset", "Length"], Rows = rows };
        }
        if (kind == KnownGuids.SourceLink)
        {
            return new MetadataRowDetails.Text
            {
                Content = reader.ReadUTF8(reader.RemainingBytes),
                Extension = ".json",
            };
        }

        if (kind == KnownGuids.EmbeddedSource)
        {
            var embeddedSourceFormat = reader.ReadInt32();

            if (embeddedSourceFormat < 0) // unknown format, show raw data as hex
            {
                return new MetadataRowDetails.Text { Content = reader.ToHexString(), Extension = null };
            }

            var embeddedSourceBytes = reader.ReadBytes(reader.RemainingBytes);
            Stream embeddedSourceByteStream = new MemoryStream(embeddedSourceBytes);

            if (embeddedSourceFormat > 0) // positive length means the data is compressed using DEFLATE
            {
                embeddedSourceByteStream = new System.IO.Compression.DeflateStream(embeddedSourceByteStream, System.IO.Compression.CompressionMode.Decompress);
            }

            using var textReader = new StreamReader(embeddedSourceByteStream, detectEncodingFromByteOrderMarks: true);
            return new MetadataRowDetails.Text
            {
                Content = textReader.ReadToEnd(),
                Extension = read.Extension,
            };
        }
        if (kind == KnownGuids.CompilationOptions)
        {
            var rows = new List<IReadOnlyList<string>>();
            while (reader.RemainingBytes > 0)
            {
                var name = ReadNullTerminatedUtf8(ref reader);
                var value = ReadNullTerminatedUtf8(ref reader);
                rows.Add([name, value]);
            }

            return new MetadataRowDetails.Table { Headers = ["Name", "Value"], Rows = rows };
        }
        if (kind == KnownGuids.CompilationMetadataReferences)
        {
            var rows = new List<IReadOnlyList<string>>();
            while (reader.RemainingBytes > 0)
            {
                var fileName = ReadNullTerminatedUtf8(ref reader);
                var aliases = ReadNullTerminatedUtf8(ref reader);
                var flags = reader.ReadByte();
                var timestamp = reader.ReadUInt32();
                var fileSize = reader.ReadUInt32();
                var mvid = reader.ReadGuid();
                rows.Add([fileName, aliases, flags.ToString(), timestamp.ToString(), fileSize.ToString(), mvid.ToString()]);
            }
            return new MetadataRowDetails.Table
            {
                Headers = ["File Name", "Aliases", "Flags", "Timestamp", "File Size", "MVID"],
                Rows = rows,
            };
        }
        if (kind == KnownGuids.TupleElementNames)
        {
            var rows = new List<IReadOnlyList<string>>();
            while (reader.RemainingBytes > 0)
            {
                var elementName = ReadNullTerminatedUtf8(ref reader);
                rows.Add([elementName]);
            }

            return new MetadataRowDetails.Table { Headers = ["Element Name"], Rows = rows };
        }
        return new MetadataRowDetails.Text { Content = reader.ToHexString(), Extension = null };
    }

    // The extension selects the syntax highlighting for the embedded source's text.
    private static string? EmbeddedSourceExtension(MetadataReader metadata, EntityHandle parent)
    {
        if (parent.Kind != HandleKind.Document)
        {
            return null;
        }

        var document = metadata.GetDocument((DocumentHandle)parent);
        if (document.Name.IsNil)
        {
            return null;
        }

        var extension = Path.GetExtension(metadata.GetString(document.Name));
        return string.IsNullOrEmpty(extension) ? null : extension;
    }

    private static string ReadNullTerminatedUtf8(ref BlobReader reader)
    {
        var length = reader.IndexOf(0);
        if (length < 0)
        {
            throw new BadImageFormatException("Missing null terminator in blob.");
        }

        var s = reader.ReadUTF8(length);
        reader.ReadByte();
        return s;
    }

    // A row's details as read: plain values, and the reader of its value's blob, which the format half decodes.
    private sealed record DetailsRead
    {
        public required Guid Kind { get; init; }

        public required BlobReader Value { get; init; }

        public required string? Extension { get; init; }
    }
}

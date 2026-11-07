// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

/// <summary>One column of a <see cref="GridSchema"/>: its cell value and tooltip, read from a row
/// and its 1-based RID.</summary>
public sealed record GridColumn(MetadataColumn Column, string? Format, Func<object, int, object?> Value, Func<object, int, object?>? Tooltip);

/// <summary>What a <see cref="MetadataGrid"/> lists: its columns, and how to read its rows.</summary>
public class GridSchema
{
    /// <summary>What names the kind of rows, the same for a table in any assembly.</summary>
    public required string Key { get; init; }

    /// <summary>The table the rows are of, for a metadata table.</summary>
    public required TableIndex? Table { get; init; }

    /// <summary>Reads every row, in order.</summary>
    public required Func<IReadOnlyList<object>> ReadRows { get; init; }

    public readonly List<GridColumn> Columns = [];

    /// <summary>The column naming each row, or -1.</summary>
    internal int NameColumn = -1;

    /// <summary>What is shown under a selected row, for a grid that shows something.</summary>
    internal Func<object, MetadataRowDetails?>? Details;
}

/// <summary>A <see cref="GridSchema"/> over rows of <typeparamref name="TRow"/>, built a column at a
/// time by <see cref="GridSchemas"/>.</summary>
public sealed class GridSchema<TRow> : GridSchema
{
    public required MetadataFile File { get; init; }
}

/// <summary>Building a <see cref="GridSchema"/> a column at a time. A table's starts with its RID and Token.</summary>
public static class GridSchemas
{
    private static IReadOnlyList<object> LoadRows<TRow>(Func<IEnumerable<TRow>> rows)
    {
        return rows().Select(static r => (object)r!).ToArray();
    }

    /// <summary>A schema named <paramref name="key"/> of what <paramref name="rows"/> reads from <paramref name="file"/>.</summary>
    public static GridSchema<TRow> CreateGrid<TRow>(string key, MetadataFile file, Func<IEnumerable<TRow>> rows)
    {
        return new GridSchema<TRow> { Key = key, Table = null, File = file, ReadRows = () => LoadRows(rows) };
    }

    /// <summary>The schema of <paramref name="table"/> of <paramref name="file"/>, which <paramref name="rows"/> reads.</summary>
    public static GridSchema<TRow> CreateTable<TRow>(TableIndex table, MetadataFile file, Func<IEnumerable<TRow>> rows)
    {
        var schema = new GridSchema<TRow> { Key = table.ToString(), Table = table, File = file, ReadRows = () => LoadRows(rows) };
        AddColumn(schema, "RID", null, (_, rid) => rid, null);
        AddColumn(schema, "Token", "X8", (_, rid) => (int)table << 24 | rid, null);
        return schema;
    }

    /// <summary><paramref name="schema"/>, with what <paramref name="columns"/> adds to it.</summary>
    public static GridSchema<TRow> AddColumns<TRow>(GridSchema<TRow> schema, Action<GridSchema<TRow>> columns)
    {
        columns(schema);
        return schema;
    }

    /// <summary>A column of what <paramref name="get"/> reads, in <paramref name="format"/>.</summary>
    public static void AddValue<TRow, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(GridSchema<TRow> schema, string name, Func<TRow, T> get, string? format = null,
        Func<TRow, object?>? tooltip = null)
    {
        AddColumn(schema, name, format, (r, _) => get(r), tooltip == null ? null : (r, _) => tooltip(r));
    }

    /// <summary>Where each row starts in the file.</summary>
    public static void AddOffset<TRow>(GridSchema<TRow> schema)
    {
        var table = schema.Table!.Value;
        var metadata = schema.File.Metadata;
        var start = schema.File.MetadataOffset + metadata.GetTableMetadataOffset(table);
        var size = metadata.GetTableRowSize(table);
        AddColumn(schema, "Offset", "X8", (_, rid) => start + size * (rid - 1), null);
    }

    /// <summary>A string of the #Strings heap, explained by its offset.</summary>
    public static void AddString<TRow>(GridSchema<TRow> schema, string name, Func<TRow, StringHandle> get)
    {
        var file = schema.File;
        AddValue(schema, name, r => file.Metadata.GetString(get(r)),
            tooltip: r => $"{MetadataTokens.GetHeapOffset(get(r)):X} \"{file.Metadata.GetString(get(r))}\"");
    }

    /// <summary>The row's name: a <see cref="AddString"/> column the row is known by.</summary>
    public static void AddName<TRow>(GridSchema<TRow> schema, Func<TRow, StringHandle> get)
    {
        schema.NameColumn = schema.Columns.Count;
        AddString(schema, "Name", get);
    }

    /// <summary>A token that links to its row, explained by what it points at unless
    /// <paramref name="tooltip"/> says otherwise.</summary>
    public static void AddHandle<TRow>(GridSchema<TRow> schema, string name, Func<TRow, EntityHandle> get, Func<TRow, object?>? tooltip = null)
    {
        var file = schema.File;
        AddColumn(schema, name, "X8", (r, _) => MetadataTokens.GetToken(get(r)),
            tooltip == null ? (r, _) => Describe(file, get(r)) : (r, _) => tooltip(r), isToken: true);
    }

    /// <summary>The start of a run of rows of <paramref name="table"/>, as a token, explained by the
    /// row it starts at or, for an empty run, by <paramref name="empty"/>.</summary>
    public static void AddList<TRow>(GridSchema<TRow> schema, string name, TableIndex table, Func<TRow, int> get, Func<TRow, EntityHandle> first, string empty)
    {
        var file = schema.File;
        AddColumn(schema, name, "X8", (r, _) => (int)table << 24 | get(r),
            (r, _) => first(r) is { IsNil: false } handle ? Describe(file, handle) : empty, isToken: true);
    }

    /// <summary>The offset of a blob in the #Blob heap.</summary>
    public static void AddBlob<TRow>(GridSchema<TRow> schema, string name, Func<TRow, BlobHandle> get, Func<TRow, object?>? tooltip = null)
    {
        AddValue(schema, name, r => MetadataTokens.GetHeapOffset(get(r)), "X8", tooltip);
    }

    /// <summary>The offset of a signature blob, explained by what <paramref name="decode"/> reads from the
    /// blob, else by the row it is the signature of.</summary>
    public static void AddSignature<TRow>(GridSchema<TRow> schema, string name, Func<TRow, BlobHandle> get, Func<TRow, string?>? decode = null)
    {
        var table = schema.Table!.Value;
        AddColumn(schema, name, "X8", (r, _) => MetadataTokens.GetHeapOffset(get(r)), (r, rid) => ExplainSignature(schema.File, r, MetadataTokens.EntityHandle(table, rid), decode), isToken: false);
    }

    private static string? ExplainSignature<TRow>(MetadataFile file, TRow row, EntityHandle handle, Func<TRow, string?>? decode)
    {
        var decoded = Decode(row, decode);
        if (decoded != null)
            return decoded;
        return Describe(file, handle);
    }

    private static string? Decode<TRow>(TRow row, Func<TRow, string?>? decode)
    {
        if (decode == null)
            return null;
        return decode(row);
    }

    /// <summary>The index of a GUID in the #GUID heap, explained by the GUID unless
    /// <paramref name="tooltip"/> says otherwise.</summary>
    public static void AddGuid<TRow>(GridSchema<TRow> schema, string name, Func<TRow, GuidHandle> get, Func<TRow, object?>? tooltip = null)
    {
        var file = schema.File;
        AddValue(schema, name, r => MetadataTokens.GetHeapOffset(get(r)), "X8",
            tooltip ?? (r => get(r) is { IsNil: false } handle ? file.Metadata.GetGuid(handle).ToString() : null));
    }

    /// <summary>What is shown under a selected row.</summary>
    public static void SetDetails<TRow>(GridSchema<TRow> schema, Func<TRow, MetadataRowDetails?> get)
    {
        schema.Details = r => get((TRow)r);
    }

    private static void AddColumn<TRow, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(GridSchema<TRow> schema, string name, string? format, Func<TRow, int, T> value,
        Func<TRow, int, object?>? tooltip, bool isToken = false)
    {
        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        var numeric = type.IsEnum || Type.GetTypeCode(type) is >= TypeCode.SByte and <= TypeCode.Decimal;
        var flags = FlagsSchemaInferer.IsFlags(typeof(T)) ? FlagsSchemaInferer.For(typeof(T)) : null;
        schema.Columns.Add(new GridColumn(new MetadataColumn(name, isToken, numeric, flags), format,
            (r, rid) => value((TRow)r, rid),
            tooltip == null ? null : (r, rid) => tooltip((TRow)r, rid)));
    }

    /// <summary>
    /// What the entity <paramref name="handle"/> points at is, e.g. <c>(AssemblyReference) System.Runtime, ...</c>;
    /// null for a nil handle.
    /// </summary>
    public static string? Describe(MetadataFile module, EntityHandle handle)
    {
        if (handle.IsNil)
            return null;
        ITextOutput output = new PlainTextOutput();
        var context = new MetadataGenericContext(default(TypeDefinitionHandle), module.Metadata);
        var metadata = module.Metadata;
        switch (handle.Kind)
        {
            case HandleKind.ModuleDefinition:
                output.Write(metadata.GetString(metadata.GetModuleDefinition().Name));
                output.Write(" (this module)");
                break;
            case HandleKind.ModuleReference:
                output.Write(metadata.GetString(metadata.GetModuleReference((ModuleReferenceHandle)handle).Name));
                break;
            case HandleKind.AssemblyReference:
                output.Write(new ICSharpCode.Decompiler.Metadata.AssemblyReference(module, (AssemblyReferenceHandle)handle).ToString());
                break;
            case HandleKind.Parameter:
                var param = metadata.GetParameter((ParameterHandle)handle);
                output.Write(param.SequenceNumber + " - " + metadata.GetString(param.Name));
                break;
            case HandleKind.EventDefinition:
                output.Write(metadata.GetString(metadata.GetEventDefinition((EventDefinitionHandle)handle).Name));
                break;
            case HandleKind.PropertyDefinition:
                output.Write(metadata.GetString(metadata.GetPropertyDefinition((PropertyDefinitionHandle)handle).Name));
                break;
            case HandleKind.AssemblyDefinition:
                output.Write(metadata.GetString(metadata.GetAssemblyDefinition().Name));
                output.Write(" (this assembly)");
                break;
            case HandleKind.AssemblyFile:
                output.Write(metadata.GetString(metadata.GetAssemblyFile((AssemblyFileHandle)handle).Name));
                break;
            case HandleKind.GenericParameter:
                output.Write(metadata.GetString(metadata.GetGenericParameter((GenericParameterHandle)handle).Name));
                break;
            case HandleKind.ManifestResource:
                output.Write(metadata.GetString(metadata.GetManifestResource((ManifestResourceHandle)handle).Name));
                break;
            case HandleKind.Document:
                output.Write(metadata.GetString(metadata.GetDocument((DocumentHandle)handle).Name));
                break;
            default:
                handle.WriteTo(module, output, context);
                break;
        }
        return "(" + handle.Kind + ") " + output;
    }
}

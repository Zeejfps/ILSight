using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>Whether a metadata token saved earlier still names a row of a module, asked of the module before the
/// library resolves it: a token past the end of its table, or of a table holding no entities, names nothing there, and
/// what the library throws resolving one that names a row is a failure (R009).</summary>
internal static class SavedTokens
{
    private static readonly TableIndex[] EntityTables =
    [
        TableIndex.TypeDef, TableIndex.TypeRef, TableIndex.TypeSpec, TableIndex.Field, TableIndex.MethodDef, TableIndex.MemberRef,
        TableIndex.MethodSpec, TableIndex.Property, TableIndex.Event,
    ];

    /// <summary>Whether <paramref name="token"/> names a row of one of <paramref name="main"/>'s tables of entities,
    /// within the rows that table has. Reads the module's table sizes.</summary>
    public static bool IsInModule(MetadataModule main, int token)
    {
        var table = (TableIndex)((uint)token >> 24);
        if (!EntityTables.Contains(table))
        {
            return false;
        }

        var row = token & 0x00FFFFFF;
        return row >= 1 && row <= main.MetadataFile.Metadata.GetTableRowCount(table);
    }
}

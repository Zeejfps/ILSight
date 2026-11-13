using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Globalization;
using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core.Metadata;

/// <summary>The tables ILSpy knows how to list, with the columns it shows for each.</summary>
internal static class MetadataTables
{
    /// <summary>
    /// The schema of table <paramref name="table"/> of <paramref name="file"/>, or null for one
    /// whose rows have no reader (AssemblyOS, AssemblyProcessor and their AssemblyRef forms).
    /// </summary>
    public static GridSchema? Create(TableIndex table, MetadataFile file)
    {
        var m = file.Metadata;

        GridSchemaBuilder<T> Table<T>(Func<IEnumerable<T>> rows)
        {
            return GridSchemas.CreateTable(table, file, rows);
        }

        IEnumerable<int> Rids()
        {
            return Enumerable.Range(1, m.GetTableRowCount(table));
        }

        string? Hex(BlobHandle blob)
        {
            if (blob.IsNil)
            {
                return null;
            }

            var bytes = m.GetBlobContent(blob);
            return bytes.ToHexString(bytes.Length);
        }

        return table switch
        {
            TableIndex.Module => GridSchemas.AddColumns(Table<ModuleDefinition>(() => [m.GetModuleDefinition()]), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "Generation", r => r.Generation);
                GridSchemas.AddName(columns, r => r.Name);
                GridSchemas.AddGuid(columns, "Mvid", r => r.Mvid, r => m.GetGuid(r.Mvid).ToString());
                GridSchemas.AddGuid(columns, "GenerationId", r => r.GenerationId);
                GridSchemas.AddGuid(columns, "BaseGenerationId", r => r.BaseGenerationId);
            }),

            TableIndex.TypeRef => GridSchemas.AddColumns(Table(() => m.TypeReferences.Select(m.GetTypeReference)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddHandle(columns, "ResolutionScope", r => r.ResolutionScope);
                GridSchemas.AddName(columns, r => r.Name);
                GridSchemas.AddString(columns, "Namespace", r => r.Namespace);
            }),

            TableIndex.TypeDef => GridSchemas.AddColumns(Table(() => m.GetTypeDefListColumns().Select(c => (Type: m.GetTypeDefinition(c.Type), c.FieldList, c.MethodList))), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "Attributes", r => r.Type.Attributes, "X8");
                GridSchemas.AddName(columns, r => r.Type.Name);
                GridSchemas.AddString(columns, "Namespace", r => r.Type.Namespace);
                GridSchemas.AddHandle(columns, "BaseType", r => r.Type.BaseType, r => BaseTypeTooltip(file, r.Type.BaseType));
                GridSchemas.AddList(columns, "FieldList", TableIndex.Field, r => r.FieldList, r => r.Type.GetFields().FirstOrDefault(),
                    "(type has no fields; the stored value is the start of its empty field list: the next type's first field row, or one past the end of the Field table)");
                GridSchemas.AddList(columns, "MethodList", TableIndex.MethodDef, r => r.MethodList, r => r.Type.GetMethods().FirstOrDefault(),
                    "(type has no methods; the stored value is the start of its empty method list: the next type's first method row, or one past the end of the MethodDef table)");
            }),

            TableIndex.Field => GridSchemas.AddColumns(Table(() => m.FieldDefinitions.Select(m.GetFieldDefinition)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "Attributes", r => r.Attributes, "X8");
                GridSchemas.AddName(columns, r => r.Name);
                GridSchemas.AddSignature(columns, table, "Signature", r => r.Signature);
            }),

            TableIndex.MethodDef => GridSchemas.AddColumns(Table(() => m.GetMethodDefParamLists().Select(c => (Method: m.GetMethodDefinition(c.Method), c.ParamList))), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "Attributes", r => r.Method.Attributes, "X8");
                GridSchemas.AddValue(columns, "ImplAttributes", r => r.Method.ImplAttributes, "X8");
                GridSchemas.AddValue(columns, "RVA", r => r.Method.RelativeVirtualAddress, "X8");
                GridSchemas.AddName(columns, r => r.Method.Name);
                GridSchemas.AddSignature(columns, table, "Signature", r => r.Method.Signature);
                GridSchemas.AddList(columns, "ParamList", TableIndex.Param, r => r.ParamList, r => r.Method.GetParameters().FirstOrDefault(),
                    "(method has no Param rows; the stored value is the start of its empty parameter list: the next method's first Param row, or one past the end of the Param table)");
            }),

            TableIndex.Param => GridSchemas.AddColumns(Table(() => Rids().Select(rid => m.GetParameter(MetadataTokens.ParameterHandle(rid)))), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "Attributes", r => r.Attributes, "X8");
                GridSchemas.AddValue(columns, "Sequence", r => r.SequenceNumber);
                GridSchemas.AddName(columns, r => r.Name);
            }),

            TableIndex.InterfaceImpl => GridSchemas.AddColumns(Table(m.GetInterfaceImplRows), columns =>
            {
                GridSchemas.AddHandle(columns, "Class", r => r.Class);
                GridSchemas.AddHandle(columns, "Interface", r => r.Interface);
            }),

            TableIndex.MemberRef => GridSchemas.AddColumns(Table(() => m.MemberReferences.Select(m.GetMemberReference)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddHandle(columns, "Parent", r => r.Parent);
                GridSchemas.AddName(columns, r => r.Name);
                GridSchemas.AddSignature(columns, table, "Signature", r => r.Signature);
            }),

            TableIndex.Constant => GridSchemas.AddColumns(Table(() => Rids().Select(rid => m.GetConstant(MetadataTokens.ConstantHandle(rid)))), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "Type", r => r.TypeCode, "X8", r => r.TypeCode.ToString());
                GridSchemas.AddHandle(columns, "Parent", r => r.Parent);
                GridSchemas.AddBlob(columns, "Value", r => r.Value, r => ConstantText(m, r));
            }),

            TableIndex.CustomAttribute => GridSchemas.AddColumns(Table(() => m.CustomAttributes.Select(m.GetCustomAttribute)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddHandle(columns, "Parent", r => r.Parent);
                GridSchemas.AddHandle(columns, "Constructor", r => r.Constructor);
                GridSchemas.AddBlob(columns, "Value", r => r.Value);
            }),

            TableIndex.FieldMarshal => GridSchemas.AddColumns(Table(m.GetFieldMarshals), columns =>
            {
                GridSchemas.AddHandle(columns, "Parent", r => r.Parent);
                GridSchemas.AddBlob(columns, "NativeType", r => r.NativeType);
            }),

            TableIndex.DeclSecurity => GridSchemas.AddColumns(Table(() => m.DeclarativeSecurityAttributes.Select(m.GetDeclarativeSecurityAttribute)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddHandle(columns, "Parent", r => r.Parent);
                GridSchemas.AddValue(columns, "Action", r => r.Action, "X8", r => r.Action.ToString());
                GridSchemas.AddBlob(columns, "PermissionSet", r => r.PermissionSet);
            }),

            TableIndex.ClassLayout => GridSchemas.AddColumns(Table(m.GetClassLayouts), columns =>
            {
                GridSchemas.AddHandle(columns, "Parent", r => r.Parent);
                GridSchemas.AddValue(columns, "PackingSize", r => (ushort)r.PackingSize);
                GridSchemas.AddValue(columns, "ClassSize", r => r.ClassSize);
            }),

            TableIndex.FieldLayout => GridSchemas.AddColumns(Table(m.GetFieldLayoutRows), columns =>
            {
                GridSchemas.AddHandle(columns, "Field", r => r.Field);
                GridSchemas.AddValue(columns, "FieldOffset", r => r.Offset, "X8");
            }),

            TableIndex.StandAloneSig => GridSchemas.AddColumns(Table(() => Rids().Select(rid => m.GetStandaloneSignature(MetadataTokens.StandaloneSignatureHandle(rid)))), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddSignature(columns, table, "Signature", r => r.Signature, r => LocalsText(file, r));
            }),

            TableIndex.EventMap => GridSchemas.AddColumns(Table(m.GetEventMaps), columns =>
            {
                GridSchemas.AddHandle(columns, "Parent", r => r.Parent);
                GridSchemas.AddHandle(columns, "EventList", r => r.EventList);
            }),

            TableIndex.Event => GridSchemas.AddColumns(Table(() => m.EventDefinitions.Select(m.GetEventDefinition)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "Attributes", r => r.Attributes, "X8");
                GridSchemas.AddName(columns, r => r.Name);
                GridSchemas.AddHandle(columns, "Type", r => r.Type);
            }),

            TableIndex.PropertyMap => GridSchemas.AddColumns(Table(m.GetPropertyMaps), columns =>
            {
                GridSchemas.AddHandle(columns, "Parent", r => r.Parent);
                GridSchemas.AddHandle(columns, "PropertyList", r => r.PropertyList);
            }),

            TableIndex.Property => GridSchemas.AddColumns(Table(() => m.PropertyDefinitions.Select(m.GetPropertyDefinition)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "Attributes", r => r.Attributes, "X8");
                GridSchemas.AddName(columns, r => r.Name);
                GridSchemas.AddSignature(columns, table, "Signature", r => r.Signature, r => PropertySignatureText(file, r));
            }),

            TableIndex.MethodSemantics => GridSchemas.AddColumns(Table(m.GetMethodSemantics), columns =>
            {
                GridSchemas.AddValue(columns, "Semantics", r => r.Semantics, "X8");
                GridSchemas.AddHandle(columns, "Method", r => r.Method);
                GridSchemas.AddHandle(columns, "Association", r => r.Association);
            }),

            TableIndex.MethodImpl => GridSchemas.AddColumns(Table(() => Rids().Select(rid => m.GetMethodImplementation(MetadataTokens.MethodImplementationHandle(rid)))), columns =>
            {
                GridSchemas.AddHandle(columns, "Type", r => r.Type);
                GridSchemas.AddHandle(columns, "MethodDeclaration", r => r.MethodDeclaration);
                GridSchemas.AddHandle(columns, "MethodBody", r => r.MethodBody);
            }),

            TableIndex.ModuleRef => GridSchemas.AddColumns(Table(() => m.GetModuleReferences().Select(m.GetModuleReference)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddName(columns, r => r.Name);
            }),

            TableIndex.TypeSpec => GridSchemas.AddColumns(Table(() => m.GetTypeSpecifications().Select(m.GetTypeSpecification)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddBlob(columns, "Signature", r => r.Signature, r =>
                {
                    ITextOutput output = new PlainTextOutput();
                    r.DecodeSignature(new DisassemblerSignatureTypeProvider(file, output), default)(ILNameSyntax.Signature);
                    return output.ToString();
                });
            }),

            TableIndex.ImplMap => GridSchemas.AddColumns(Table(m.GetImplMaps), columns =>
            {
                GridSchemas.AddValue(columns, "MappingFlags", r => r.MappingFlags, "X8");
                GridSchemas.AddHandle(columns, "MemberForwarded", r => r.MemberForwarded);
                GridSchemas.AddString(columns, "ImportName", r => r.ImportName);
                GridSchemas.AddHandle(columns, "ImportScope", r => r.ImportScope);
            }),

            TableIndex.FieldRva => GridSchemas.AddColumns(Table(m.GetFieldRvas), columns =>
            {
                GridSchemas.AddHandle(columns, "Field", r => r.Field);
                GridSchemas.AddValue(columns, "RVA", r => r.RelativeVirtualAddress, "X8");
            }),

            TableIndex.Assembly => GridSchemas.AddColumns(Table<AssemblyDefinition>(() => m.IsAssembly ? [m.GetAssemblyDefinition()] : []), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "HashAlgorithm", r => r.HashAlgorithm, "X4", r => r.HashAlgorithm.ToString());
                GridSchemas.AddValue(columns, "Flags", r => r.Flags, "X4");
                GridSchemas.AddValue(columns, "Version", r => r.Version);
                GridSchemas.AddName(columns, r => r.Name);
                GridSchemas.AddString(columns, "Culture", r => r.Culture);
            }),

            TableIndex.AssemblyRef => GridSchemas.AddColumns(Table(() => m.AssemblyReferences.Select(m.GetAssemblyReference)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "Version", r => r.Version);
                GridSchemas.AddValue(columns, "Flags", r => r.Flags, "X8");
                GridSchemas.AddBlob(columns, "PublicKeyOrToken", r => r.PublicKeyOrToken, r => Hex(r.PublicKeyOrToken));
                GridSchemas.AddName(columns, r => r.Name);
                GridSchemas.AddString(columns, "Culture", r => r.Culture);
            }),

            TableIndex.File => GridSchemas.AddColumns(Table(() => m.AssemblyFiles.Select(m.GetAssemblyFile)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "Attributes", r => r.ContainsMetadata ? 1 : 0, "X8", r => r.ContainsMetadata ? "ContainsMetaData" : "ContainsNoMetaData");
                GridSchemas.AddName(columns, r => r.Name);
                GridSchemas.AddBlob(columns, "HashValue", r => r.HashValue, r => Hex(r.HashValue));
            }),

            TableIndex.ExportedType => GridSchemas.AddColumns(Table(() => m.ExportedTypes.Select(m.GetExportedType)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "Attributes", r => r.Attributes, "X8");
                GridSchemas.AddValue(columns, "TypeDefId", r => r.GetTypeDefinitionId(), "X8");
                GridSchemas.AddString(columns, "TypeName", r => r.Name);
                GridSchemas.AddString(columns, "TypeNamespace", r => r.Namespace);
                GridSchemas.AddHandle(columns, "Implementation", r => r.Implementation);
            }),

            TableIndex.ManifestResource => GridSchemas.AddColumns(Table(() => m.ManifestResources.Select(m.GetManifestResource)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "Attributes", r => r.Attributes, "X8");
                GridSchemas.AddName(columns, r => r.Name);
                GridSchemas.AddHandle(columns, "Implementation", r => r.Implementation);
            }),

            TableIndex.NestedClass => GridSchemas.AddColumns(Table(m.GetNestedClasses), columns =>
            {
                GridSchemas.AddHandle(columns, "NestedClass", r => r.NestedClass);
                GridSchemas.AddHandle(columns, "EnclosingClass", r => r.EnclosingClass);
            }),

            TableIndex.GenericParam => GridSchemas.AddColumns(Table(() => Rids().Select(rid => m.GetGenericParameter(MetadataTokens.GenericParameterHandle(rid)))), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddValue(columns, "Number", r => r.Index);
                GridSchemas.AddValue(columns, "Attributes", r => r.Attributes, "X8");
                GridSchemas.AddHandle(columns, "Owner", r => r.Parent);
                GridSchemas.AddName(columns, r => r.Name);
            }),

            TableIndex.MethodSpec => GridSchemas.AddColumns(Table(() => m.GetMethodSpecifications().Select(m.GetMethodSpecification)), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddHandle(columns, "Method", r => r.Method);
                GridSchemas.AddBlob(columns, "Signature", r => r.Signature, r =>
                {
                    ITextOutput output = new PlainTextOutput();
                    var first = true;
                    foreach (var type in r.DecodeSignature(new DisassemblerSignatureTypeProvider(file, output), default))
                    {
                        if (first)
                        {
                            first = false;
                        }
                        else
                        {
                            output.Write(", ");
                        }

                        type(ILNameSyntax.TypeName);
                    }
                    return output.ToString();
                });
            }),

            TableIndex.GenericParamConstraint => GridSchemas.AddColumns(Table(() => Rids().Select(rid => m.GetGenericParameterConstraint(MetadataTokens.GenericParameterConstraintHandle(rid)))), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddHandle(columns, "Owner", r => r.Parameter, r =>
                {
                    ITextOutput output = new PlainTextOutput();
                    var p = m.GetGenericParameter(r.Parameter);
                    output.Write("parameter " + p.Index + (p.Name.IsNil ? "" : " (" + m.GetString(p.Name) + ")") + " of ");
                    p.Parent.WriteTo(file, output, default);
                    return output.ToString();
                });
                GridSchemas.AddHandle(columns, "Type", r => r.Type);
            }),

            TableIndex.Document => GridSchemas.AddColumns(Table(() => m.Documents.Select(m.GetDocument)), columns =>
            {
                GridSchemas.AddValue(columns, "Name", r => m.GetString(r.Name), tooltip: r => $"{MetadataTokens.GetHeapOffset(r.Name):X} \"{m.GetString(r.Name)}\"");
                GridSchemas.AddGuid(columns, "HashAlgorithm", r => r.HashAlgorithm, r =>
                {
                    if (r.HashAlgorithm.IsNil)
                    {
                        return null;
                    }

                    var guid = m.GetGuid(r.HashAlgorithm);
                    if (guid == KnownGuids.HashAlgorithmSHA1)
                    {
                        return "SHA1 [ff1816ec-aa5e-4d10-87f7-6f4963833460]";
                    }

                    if (guid == KnownGuids.HashAlgorithmSHA256)
                    {
                        return "SHA256 [8829d00f-11b8-4213-878b-770e8597ac16]";
                    }

                    return "Unknown [" + guid + "]";
                });
                GridSchemas.AddBlob(columns, "Hash", r => r.Hash, r => Hex(r.Hash));
                GridSchemas.AddGuid(columns, "Language", r => r.Language, r =>
                {
                    if (r.Language.IsNil)
                    {
                        return null;
                    }

                    var guid = m.GetGuid(r.Language);
                    if (guid == KnownGuids.CSharpLanguageGuid)
                    {
                        return "Visual C# [3f5162f8-07c6-11d3-9053-00c04fa302a1]";
                    }

                    if (guid == KnownGuids.VBLanguageGuid)
                    {
                        return "Visual Basic [3a12d0b8-c26c-11d0-b442-00a0244a1dd2]";
                    }

                    if (guid == KnownGuids.FSharpLanguageGuid)
                    {
                        return "Visual F# [ab4f38c9-b6e6-43ba-be3b-58080b2ccce3]";
                    }

                    return "Unknown [" + guid + "]";
                });
            }),

            TableIndex.MethodDebugInformation => GridSchemas.AddColumns(Table(() => m.MethodDebugInformation.Select(m.GetMethodDebugInformation)), columns =>
            {
                GridSchemas.AddHandle(columns, "Document", r => r.Document, r =>
                {
                    if (r.Document.IsNil)
                    {
                        return null;
                    }

                    var document = m.GetDocument(r.Document);
                    return $"{MetadataTokens.GetHeapOffset(document.Name):X} \"{m.GetString(document.Name)}\"";
                });
                GridSchemas.AddBlob(columns, "SequencePoints", r => r.SequencePointsBlob, r =>
                {
                    if (r.SequencePointsBlob.IsNil)
                    {
                        return null;
                    }

                    var sb = new StringBuilder();
                    foreach (var p in r.GetSequencePoints())
                    {
                        sb.AppendLine($"document='{MetadataTokens.GetToken(p.Document):X8}', offset={p.Offset}, start={p.StartLine};{p.StartColumn}, end={p.EndLine};{p.EndColumn}, hidden={p.IsHidden}");
                    }

                    return sb.ToString().TrimEnd();
                });
                GridSchemas.AddHandle(columns, "LocalSignature", r => r.LocalSignature, r =>
                {
                    if (r.LocalSignature.IsNil)
                    {
                        return null;
                    }

                    return LocalsText(file, m.GetStandaloneSignature(r.LocalSignature));
                });
            }),

            TableIndex.LocalScope => GridSchemas.AddColumns(Table(() => m.LocalScopes.Select(m.GetLocalScope)), columns =>
            {
                GridSchemas.AddHandle(columns, "Method", r => r.Method);
                GridSchemas.AddHandle(columns, "ImportScope", r => r.ImportScope, NoTooltip);
                GridSchemas.AddHandle(columns, "VariableList", r => r.GetLocalVariables().FirstOrDefault(), NoTooltip);
                GridSchemas.AddHandle(columns, "ConstantList", r => r.GetLocalConstants().FirstOrDefault(), NoTooltip);
                GridSchemas.AddValue(columns, "StartOffset", r => r.StartOffset);
                GridSchemas.AddValue(columns, "Length", r => r.Length);
            }),

            TableIndex.LocalVariable => GridSchemas.AddColumns(Table(() => m.LocalVariables.Select(m.GetLocalVariable)), columns =>
            {
                GridSchemas.AddValue(columns, "Attributes", r => r.Attributes, "X8");
                GridSchemas.AddValue(columns, "Index", r => r.Index);
                GridSchemas.AddName(columns, r => r.Name);
            }),

            TableIndex.LocalConstant => GridSchemas.AddColumns(Table(() => m.LocalConstants.Select(m.GetLocalConstant)), columns =>
            {
                GridSchemas.AddName(columns, r => r.Name);
                GridSchemas.AddBlob(columns, "Signature", r => r.Signature);
            }),

            TableIndex.ImportScope => GridSchemas.AddColumns(Table(() => m.ImportScopes.Select(m.GetImportScope)), columns =>
            {
                GridSchemas.AddHandle(columns, "Parent", r => r.Parent, NoTooltip);
                GridSchemas.AddBlob(columns, "Imports", r => r.ImportsBlob);
            }),

            TableIndex.StateMachineMethod => GridSchemas.AddColumns(Table(() => StateMachineMethods(m)), columns =>
            {
                GridSchemas.AddHandle(columns, "MoveNextMethod", r => r.MoveNext);
                GridSchemas.AddHandle(columns, "KickoffMethod", r => r.Kickoff);
            }),

            TableIndex.CustomDebugInformation => CustomDebugInformationTable.Columns(Table(() => m.CustomDebugInformation.Select(m.GetCustomDebugInformation)), file),

            TableIndex.EncLog => GridSchemas.AddColumns(Table(m.GetEditAndContinueLogEntries), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddHandle(columns, "Handle", r => r.Handle);
                GridSchemas.AddValue(columns, "FuncCode", r => r.Operation, "X8", r => r.Operation.ToString());
            }),

            TableIndex.EncMap => GridSchemas.AddColumns(Table(m.GetEditAndContinueMapEntries), columns =>
            {
                GridSchemas.AddOffset(columns, table);
                GridSchemas.AddHandle(columns, "Handle", r => r);
            }),

            TableIndex.FieldPtr or TableIndex.MethodPtr or TableIndex.ParamPtr or TableIndex.EventPtr or TableIndex.PropertyPtr => GridSchemas.AddColumns(Table(() => m.GetPtrRows(table)), columns =>
            {
                GridSchemas.AddHandle(columns, "Handle", r => r);
            }),

            _ => null,
        };
    }

    private static object? NoTooltip<T>(T row)
    {
        return null;
    }

    private const char StringQuote = '"';
    private const char CharQuote = '\'';

    private static string? ConstantText(MetadataReader metadata, Constant constant)
    {
        try
        {
            var value = metadata.GetBlobReader(constant.Value).ReadConstant(constant.TypeCode);
            return LiteralText(value);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string LiteralText(object? value)
    {
        return value switch
        {
            null => "null",
            string text => Quote(text, StringQuote),
            char c => Quote(c.ToString(), CharQuote),
            true => "true",
            false => "false",
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }

    private static string Quote(string text, char quote)
    {
        return quote + DisassemblerHelpers.EscapeString(text) + quote;
    }

    private static string? LocalsText(MetadataFile file, StandaloneSignature signature)
    {
        var kind = signature.GetKind();
        if (kind != StandaloneSignatureKind.LocalVariables)
        {
            return null;
        }

        try
        {
            return WriteLocals(file, signature);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static string WriteLocals(MetadataFile file, StandaloneSignature signature)
    {
        var output = new PlainTextOutput();
        var context = new MetadataGenericContext(default(TypeDefinitionHandle), file.Metadata);
        var provider = new DisassemblerSignatureTypeProvider(file, output);
        var locals = signature.DecodeLocalSignature(provider, context);
        for (var i = 0; i < locals.Length; i++)
        {
            if (i > 0)
            {
                output.WriteLine();
            }

            output.Write($"[{i}] ");
            locals[i](ILNameSyntax.Signature);
        }
        return output.ToString();
    }

    private static string? PropertySignatureText(MetadataFile file, PropertyDefinition property)
    {
        try
        {
            return WritePropertySignature(file, property);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static string WritePropertySignature(MetadataFile file, PropertyDefinition property)
    {
        var output = new PlainTextOutput();
        var metadata = file.Metadata;
        var declaringType = FindDeclaringType(metadata, property);
        // MetadataGenericContext takes a nil handle for "no declaring type".
        var context = new MetadataGenericContext(declaringType.GetValueOrDefault(), metadata);
        var signature = property.DecodeSignature(new DisassemblerSignatureTypeProvider(file, output), context);
        if (signature.Header.IsInstance)
        {
            output.Write("instance ");
        }

        signature.ReturnType(ILNameSyntax.Signature);
        output.Write(" ");
        output.Write(DisassemblerHelpers.Escape(metadata.GetString(property.Name)));
        output.Write("(");
        WriteTypes(output, signature.ParameterTypes);
        output.Write(")");
        return output.ToString();
    }

    // Found so that the declaring type's generic parameters are written by name.
    private static TypeDefinitionHandle? FindDeclaringType(MetadataReader metadata, PropertyDefinition property)
    {
        var accessor = GetAccessor(property);
        if (accessor.IsNil)
        {
            return null;
        }

        return metadata.GetMethodDefinition(accessor).GetDeclaringType();
    }

    private static MethodDefinitionHandle GetAccessor(PropertyDefinition property)
    {
        var accessors = property.GetAccessors();
        if (!accessors.Getter.IsNil)
        {
            return accessors.Getter;
        }

        if (!accessors.Setter.IsNil)
        {
            return accessors.Setter;
        }

        return accessors.Others.FirstOrDefault();
    }

    private static void WriteTypes(ITextOutput output, IReadOnlyList<Action<ILNameSyntax>> types)
    {
        for (var i = 0; i < types.Count; i++)
        {
            if (i > 0)
            {
                output.Write(", ");
            }

            types[i](ILNameSyntax.Signature);
        }
    }

    private static string? BaseTypeTooltip(MetadataFile file, EntityHandle baseType)
    {
        if (baseType.IsNil)
        {
            return null;
        }

        var output = new PlainTextOutput();
        var provider = new DisassemblerSignatureTypeProvider(file, output);
        switch (baseType.Kind)
        {
            case HandleKind.TypeDefinition:
                provider.GetTypeFromDefinition(file.Metadata, (TypeDefinitionHandle)baseType, 0)(ILNameSyntax.Signature);
                return output.ToString();
            case HandleKind.TypeReference:
                provider.GetTypeFromReference(file.Metadata, (TypeReferenceHandle)baseType, 0)(ILNameSyntax.Signature);
                return output.ToString();
            case HandleKind.TypeSpecification:
                provider.GetTypeFromSpecification(file.Metadata, new MetadataGenericContext(default(TypeDefinitionHandle), file.Metadata), (TypeSpecificationHandle)baseType, 0)(ILNameSyntax.Signature);
                return output.ToString();
            default:
                return null;
        }
    }

    // Read from the table: System.Reflection.Metadata has no reader for StateMachineMethod rows.
    private static IEnumerable<(MethodDefinitionHandle MoveNext, MethodDefinitionHandle Kickoff)> StateMachineMethods(MetadataReader metadata)
    {
        var length = metadata.GetTableRowCount(TableIndex.StateMachineMethod);
        var reader = metadata.AsBlobReader();
        reader.Offset = metadata.GetTableMetadataOffset(TableIndex.StateMachineMethod);
        // A MethodDef index is two bytes or four, as half the row is.
        var methodDefSize = metadata.GetTableRowSize(TableIndex.StateMachineMethod) / 2;
        var rows = new List<(MethodDefinitionHandle, MethodDefinitionHandle)>(length);
        for (var rid = 1; rid <= length; rid++)
        {
            var moveNext = MetadataTokens.MethodDefinitionHandle(methodDefSize == 2 ? reader.ReadUInt16() : reader.ReadInt32());
            var kickoff = MetadataTokens.MethodDefinitionHandle(methodDefSize == 2 ? reader.ReadUInt16() : reader.ReadInt32());
            rows.Add((moveNext, kickoff));
        }
        return rows;
    }

    /// <summary>The folder as comment lines: its title, kind and version, a debug header's id and entry
    /// point, and how many rows each table has.</summary>
    internal static IEnumerable<string> ListFolderLines(MetadataFolderInfo folder)
    {
        var metadata = folder.File.Metadata;
        yield return folder.Title;
        yield return "MetadataKind: " + metadata.MetadataKind;
        yield return "MetadataVersion: " + metadata.MetadataVersion;
        var header = metadata.DebugMetadataHeader;
        if (header is not null)
        {
            yield return "";
            yield return "Header:";
            yield return "Id: " + Convert.ToHexStringLower(header.Id.AsSpan());
            yield return "EntryPoint: " + MetadataTokens.GetToken(header.EntryPoint).ToString("X8");
        }
        yield return "";
        yield return "Tables:";
        foreach (var line in ListRowCounts(folder))
        {
            yield return line;
        }
    }

    /// <summary>The Tables folder as comment lines: how many rows each table has.</summary>
    internal static IEnumerable<string> ListTablesLines(MetadataFolderInfo folder)
    {
        return ListRowCounts(folder).Prepend("Tables");
    }

    private static IEnumerable<string> ListRowCounts(MetadataFolderInfo folder)
    {
        var metadata = folder.File.Metadata;
        foreach (var table in Enum.GetValues<TableIndex>())
        {
            var count = metadata.GetTableRowCount(table);
            if (count > 0)
            {
                yield return $"{(byte)table:X2} {table}: {count} rows";
            }
        }
    }

    /// <summary>The grid shown, made the first time it is asked for with at least <paramref name="firstRows"/> rows
    /// read (<see cref="LazyGrid"/>); null for a part shown as text.</summary>
    internal static MetadataGrid? EnsureGrid(MetadataTableInfo table, int firstRows)
    {
        return table.Part.Match<int, MetadataGrid?>(firstRows,
            static (rows, grid) => grid.View.Ensure(rows),
            static (rows, directory) => directory.View.Ensure(rows),
            static (_, _) => null,
            static (_, _) => null,
            static (_, _) => null);
    }

    internal static MetadataTableInfo CreateGridPart(LoadedModule module, MetadataFile file, string title, MetadataPartKey key, MetadataGridRole role,
        GridSchema schema)
    {
        return new MetadataTableInfo
        {
            Module = module,
            File = file,
            Title = title,
            Key = key,
            Part = new MetadataPart.Grid
            {
                Role = role,
                Schema = schema,
                View = new LazyGrid { Title = title, Module = module, File = file, Schema = schema },
            },
        };
    }

    internal static MetadataTableInfo CreateDebugDirectoryPart(LoadedModule module, PEFile file)
    {
        const string title = "Debug Directory";
        var schema = PEHeaderEntries.DebugDirectory(file);
        return new MetadataTableInfo
        {
            Module = module,
            File = file,
            Title = title,
            Key = new MetadataPartKey.DebugDirectory(),
            Part = new MetadataPart.DebugDirectory
            {
                File = file,
                View = new LazyGrid { Title = title, Module = module, File = file, Schema = schema },
            },
        };
    }

    internal static MetadataTableInfo CreateRowCountPart(LoadedModule module, MetadataFile file, TableIndex table, int rows)
    {
        return new MetadataTableInfo
        {
            Module = module,
            File = file,
            Title = $"{(byte)table:X2} {table} ({rows})",
            Key = new MetadataPartKey.Table(table),
            Part = new MetadataPart.RowCount
            {
                Table = table,
                Rows = rows,
            },
        };
    }

    internal static MetadataTableInfo CreateDecodedEntryPart(LoadedModule module, PEFile file, int index, DebugDirectoryEntry entry, IReadOnlyList<string> lines)
    {
        return new MetadataTableInfo
        {
            Module = module,
            File = file,
            Title = entry.Type.ToString(),
            Key = new MetadataPartKey.DebugEntry
            {
                Index = index,
                Type = entry.Type,
            },
            Part = new MetadataPart.DecodedDebugEntry(lines),
        };
    }

    internal static MetadataTableInfo CreateRawEntryPart(LoadedModule module, PEFile file, int index, DebugDirectoryEntry entry)
    {
        return new MetadataTableInfo
        {
            Module = module,
            File = file,
            Title = entry.Type.ToString(),
            Key = new MetadataPartKey.DebugEntry
            {
                Index = index,
                Type = entry.Type,
            },
            Part = new MetadataPart.RawDebugEntry
            {
                File = file,
                Entry = entry,
            },
        };
    }

    /// <summary>A part shown as text, as comment lines: a table's row count, or a debug entry decoded or
    /// as its raw bytes in hex; a grid's title alone.</summary>
    internal static IEnumerable<string> ListPartLines(MetadataTableInfo table)
    {
        return table.Part.Match<MetadataTableInfo, IEnumerable<string>>(table,
            static (info, _) => [info.Title],
            static (info, _) => [info.Title],
            static (_, count) => [$"{count.Table}: {count.Rows} rows"],
            static (info, decoded) => decoded.Lines.Prepend(info.Title),
            static (info, raw) => ListRawEntryLines(info, raw));
    }

    private static IEnumerable<string> ListRawEntryLines(MetadataTableInfo table, MetadataPart.RawDebugEntry raw)
    {
        yield return table.Title;
        var entry = raw.Entry;
        if (entry.DataSize > 0)
        {
            yield return $"Raw Data ({entry.DataSize}):";
            var reader = raw.File.Reader;
            var offset = reader.IsLoadedImage ? entry.DataRelativeVirtualAddress : entry.DataPointer;
            var data = reader.GetEntireImage().GetContent(offset, entry.DataSize);
            yield return Convert.ToHexStringLower(data.AsSpan());
        }
        else
        {
            yield return "(no data)";
        }
    }
}

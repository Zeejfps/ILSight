// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using static ILSight.Core.Metadata.GridSchemas;
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

        GridSchema<T> Table<T>(Func<IEnumerable<T>> rows)
        {
            return CreateTable(table, file, rows);
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
            TableIndex.Module => AddColumns(Table<ModuleDefinition>(() => [m.GetModuleDefinition()]), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "Generation", r => r.Generation);
                AddName(columns, r => r.Name);
                AddGuid(columns, "Mvid", r => r.Mvid, r => m.GetGuid(r.Mvid).ToString());
                AddGuid(columns, "GenerationId", r => r.GenerationId);
                AddGuid(columns, "BaseGenerationId", r => r.BaseGenerationId);
            }),

            TableIndex.TypeRef => AddColumns(Table(() => m.TypeReferences.Select(m.GetTypeReference)), columns =>
            {
                AddOffset(columns, table);
                AddHandle(columns, "ResolutionScope", r => r.ResolutionScope);
                AddName(columns, r => r.Name);
                AddString(columns, "Namespace", r => r.Namespace);
            }),

            TableIndex.TypeDef => AddColumns(Table(() => m.GetTypeDefListColumns().Select(c => (Type: m.GetTypeDefinition(c.Type), c.FieldList, c.MethodList))), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "Attributes", r => r.Type.Attributes, "X8");
                AddName(columns, r => r.Type.Name);
                AddString(columns, "Namespace", r => r.Type.Namespace);
                AddHandle(columns, "BaseType", r => r.Type.BaseType, r => BaseTypeTooltip(file, r.Type.BaseType));
                AddList(columns, "FieldList", TableIndex.Field, r => r.FieldList, r => r.Type.GetFields().FirstOrDefault(),
                    "(type has no fields; the stored value is the start of its empty field list: the next type's first field row, or one past the end of the Field table)");
                AddList(columns, "MethodList", TableIndex.MethodDef, r => r.MethodList, r => r.Type.GetMethods().FirstOrDefault(),
                    "(type has no methods; the stored value is the start of its empty method list: the next type's first method row, or one past the end of the MethodDef table)");
            }),

            TableIndex.Field => AddColumns(Table(() => m.FieldDefinitions.Select(m.GetFieldDefinition)), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "Attributes", r => r.Attributes, "X8");
                AddName(columns, r => r.Name);
                AddSignature(columns, table, "Signature", r => r.Signature);
            }),

            TableIndex.MethodDef => AddColumns(Table(() => m.GetMethodDefParamLists().Select(c => (Method: m.GetMethodDefinition(c.Method), c.ParamList))), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "Attributes", r => r.Method.Attributes, "X8");
                AddValue(columns, "ImplAttributes", r => r.Method.ImplAttributes, "X8");
                AddValue(columns, "RVA", r => r.Method.RelativeVirtualAddress, "X8");
                AddName(columns, r => r.Method.Name);
                AddSignature(columns, table, "Signature", r => r.Method.Signature);
                AddList(columns, "ParamList", TableIndex.Param, r => r.ParamList, r => r.Method.GetParameters().FirstOrDefault(),
                    "(method has no Param rows; the stored value is the start of its empty parameter list: the next method's first Param row, or one past the end of the Param table)");
            }),

            TableIndex.Param => AddColumns(Table(() => Rids().Select(rid => m.GetParameter(MetadataTokens.ParameterHandle(rid)))), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "Attributes", r => r.Attributes, "X8");
                AddValue(columns, "Sequence", r => r.SequenceNumber);
                AddName(columns, r => r.Name);
            }),

            TableIndex.InterfaceImpl => AddColumns(Table(m.GetInterfaceImplRows), columns =>
            {
                AddHandle(columns, "Class", r => r.Class);
                AddHandle(columns, "Interface", r => r.Interface);
            }),

            TableIndex.MemberRef => AddColumns(Table(() => m.MemberReferences.Select(m.GetMemberReference)), columns =>
            {
                AddOffset(columns, table);
                AddHandle(columns, "Parent", r => r.Parent);
                AddName(columns, r => r.Name);
                AddSignature(columns, table, "Signature", r => r.Signature);
            }),

            TableIndex.Constant => AddColumns(Table(() => Rids().Select(rid => m.GetConstant(MetadataTokens.ConstantHandle(rid)))), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "Type", r => r.TypeCode, "X8", r => r.TypeCode.ToString());
                AddHandle(columns, "Parent", r => r.Parent);
                AddBlob(columns, "Value", r => r.Value, r => ConstantText(m, r));
            }),

            TableIndex.CustomAttribute => AddColumns(Table(() => m.CustomAttributes.Select(m.GetCustomAttribute)), columns =>
            {
                AddOffset(columns, table);
                AddHandle(columns, "Parent", r => r.Parent);
                AddHandle(columns, "Constructor", r => r.Constructor);
                AddBlob(columns, "Value", r => r.Value);
            }),

            TableIndex.FieldMarshal => AddColumns(Table(m.GetFieldMarshals), columns =>
            {
                AddHandle(columns, "Parent", r => r.Parent);
                AddBlob(columns, "NativeType", r => r.NativeType);
            }),

            TableIndex.DeclSecurity => AddColumns(Table(() => m.DeclarativeSecurityAttributes.Select(m.GetDeclarativeSecurityAttribute)), columns =>
            {
                AddOffset(columns, table);
                AddHandle(columns, "Parent", r => r.Parent);
                AddValue(columns, "Action", r => r.Action, "X8", r => r.Action.ToString());
                AddBlob(columns, "PermissionSet", r => r.PermissionSet);
            }),

            TableIndex.ClassLayout => AddColumns(Table(m.GetClassLayouts), columns =>
            {
                AddHandle(columns, "Parent", r => r.Parent);
                AddValue(columns, "PackingSize", r => (ushort)r.PackingSize);
                AddValue(columns, "ClassSize", r => r.ClassSize);
            }),

            TableIndex.FieldLayout => AddColumns(Table(m.GetFieldLayoutRows), columns =>
            {
                AddHandle(columns, "Field", r => r.Field);
                AddValue(columns, "FieldOffset", r => r.Offset, "X8");
            }),

            TableIndex.StandAloneSig => AddColumns(Table(() => Rids().Select(rid => m.GetStandaloneSignature(MetadataTokens.StandaloneSignatureHandle(rid)))), columns =>
            {
                AddOffset(columns, table);
                AddSignature(columns, table, "Signature", r => r.Signature, r => LocalsText(file, r));
            }),

            TableIndex.EventMap => AddColumns(Table(m.GetEventMaps), columns =>
            {
                AddHandle(columns, "Parent", r => r.Parent);
                AddHandle(columns, "EventList", r => r.EventList);
            }),

            TableIndex.Event => AddColumns(Table(() => m.EventDefinitions.Select(m.GetEventDefinition)), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "Attributes", r => r.Attributes, "X8");
                AddName(columns, r => r.Name);
                AddHandle(columns, "Type", r => r.Type);
            }),

            TableIndex.PropertyMap => AddColumns(Table(m.GetPropertyMaps), columns =>
            {
                AddHandle(columns, "Parent", r => r.Parent);
                AddHandle(columns, "PropertyList", r => r.PropertyList);
            }),

            TableIndex.Property => AddColumns(Table(() => m.PropertyDefinitions.Select(m.GetPropertyDefinition)), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "Attributes", r => r.Attributes, "X8");
                AddName(columns, r => r.Name);
                AddSignature(columns, table, "Signature", r => r.Signature, r => PropertySignatureText(file, r));
            }),

            TableIndex.MethodSemantics => AddColumns(Table(m.GetMethodSemantics), columns =>
            {
                AddValue(columns, "Semantics", r => r.Semantics, "X8");
                AddHandle(columns, "Method", r => r.Method);
                AddHandle(columns, "Association", r => r.Association);
            }),

            TableIndex.MethodImpl => AddColumns(Table(() => Rids().Select(rid => m.GetMethodImplementation(MetadataTokens.MethodImplementationHandle(rid)))), columns =>
            {
                AddHandle(columns, "Type", r => r.Type);
                AddHandle(columns, "MethodDeclaration", r => r.MethodDeclaration);
                AddHandle(columns, "MethodBody", r => r.MethodBody);
            }),

            TableIndex.ModuleRef => AddColumns(Table(() => m.GetModuleReferences().Select(m.GetModuleReference)), columns =>
            {
                AddOffset(columns, table);
                AddName(columns, r => r.Name);
            }),

            TableIndex.TypeSpec => AddColumns(Table(() => m.GetTypeSpecifications().Select(m.GetTypeSpecification)), columns =>
            {
                AddOffset(columns, table);
                AddBlob(columns, "Signature", r => r.Signature, r =>
                {
                    ITextOutput output = new PlainTextOutput();
                    r.DecodeSignature(new DisassemblerSignatureTypeProvider(file, output), default)(ILNameSyntax.Signature);
                    return output.ToString();
                });
            }),

            TableIndex.ImplMap => AddColumns(Table(m.GetImplMaps), columns =>
            {
                AddValue(columns, "MappingFlags", r => r.MappingFlags, "X8");
                AddHandle(columns, "MemberForwarded", r => r.MemberForwarded);
                AddString(columns, "ImportName", r => r.ImportName);
                AddHandle(columns, "ImportScope", r => r.ImportScope);
            }),

            TableIndex.FieldRva => AddColumns(Table(m.GetFieldRvas), columns =>
            {
                AddHandle(columns, "Field", r => r.Field);
                AddValue(columns, "RVA", r => r.RelativeVirtualAddress, "X8");
            }),

            TableIndex.Assembly => AddColumns(Table<AssemblyDefinition>(() => m.IsAssembly ? [m.GetAssemblyDefinition()] : []), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "HashAlgorithm", r => r.HashAlgorithm, "X4", r => r.HashAlgorithm.ToString());
                AddValue(columns, "Flags", r => r.Flags, "X4");
                AddValue(columns, "Version", r => r.Version);
                AddName(columns, r => r.Name);
                AddString(columns, "Culture", r => r.Culture);
            }),

            TableIndex.AssemblyRef => AddColumns(Table(() => m.AssemblyReferences.Select(m.GetAssemblyReference)), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "Version", r => r.Version);
                AddValue(columns, "Flags", r => r.Flags, "X8");
                AddBlob(columns, "PublicKeyOrToken", r => r.PublicKeyOrToken, r => Hex(r.PublicKeyOrToken));
                AddName(columns, r => r.Name);
                AddString(columns, "Culture", r => r.Culture);
            }),

            TableIndex.File => AddColumns(Table(() => m.AssemblyFiles.Select(m.GetAssemblyFile)), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "Attributes", r => r.ContainsMetadata ? 1 : 0, "X8", r => r.ContainsMetadata ? "ContainsMetaData" : "ContainsNoMetaData");
                AddName(columns, r => r.Name);
                AddBlob(columns, "HashValue", r => r.HashValue, r => Hex(r.HashValue));
            }),

            TableIndex.ExportedType => AddColumns(Table(() => m.ExportedTypes.Select(m.GetExportedType)), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "Attributes", r => r.Attributes, "X8");
                AddValue(columns, "TypeDefId", r => r.GetTypeDefinitionId(), "X8");
                AddString(columns, "TypeName", r => r.Name);
                AddString(columns, "TypeNamespace", r => r.Namespace);
                AddHandle(columns, "Implementation", r => r.Implementation);
            }),

            TableIndex.ManifestResource => AddColumns(Table(() => m.ManifestResources.Select(m.GetManifestResource)), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "Attributes", r => r.Attributes, "X8");
                AddName(columns, r => r.Name);
                AddHandle(columns, "Implementation", r => r.Implementation);
            }),

            TableIndex.NestedClass => AddColumns(Table(m.GetNestedClasses), columns =>
            {
                AddHandle(columns, "NestedClass", r => r.NestedClass);
                AddHandle(columns, "EnclosingClass", r => r.EnclosingClass);
            }),

            TableIndex.GenericParam => AddColumns(Table(() => Rids().Select(rid => m.GetGenericParameter(MetadataTokens.GenericParameterHandle(rid)))), columns =>
            {
                AddOffset(columns, table);
                AddValue(columns, "Number", r => r.Index);
                AddValue(columns, "Attributes", r => r.Attributes, "X8");
                AddHandle(columns, "Owner", r => r.Parent);
                AddName(columns, r => r.Name);
            }),

            TableIndex.MethodSpec => AddColumns(Table(() => m.GetMethodSpecifications().Select(m.GetMethodSpecification)), columns =>
            {
                AddOffset(columns, table);
                AddHandle(columns, "Method", r => r.Method);
                AddBlob(columns, "Signature", r => r.Signature, r =>
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

            TableIndex.GenericParamConstraint => AddColumns(Table(() => Rids().Select(rid => m.GetGenericParameterConstraint(MetadataTokens.GenericParameterConstraintHandle(rid)))), columns =>
            {
                AddOffset(columns, table);
                AddHandle(columns, "Owner", r => r.Parameter, r =>
                {
                    ITextOutput output = new PlainTextOutput();
                    var p = m.GetGenericParameter(r.Parameter);
                    output.Write("parameter " + p.Index + (p.Name.IsNil ? "" : " (" + m.GetString(p.Name) + ")") + " of ");
                    p.Parent.WriteTo(file, output, default);
                    return output.ToString();
                });
                AddHandle(columns, "Type", r => r.Type);
            }),

            TableIndex.Document => AddColumns(Table(() => m.Documents.Select(m.GetDocument)), columns =>
            {
                AddValue(columns, "Name", r => m.GetString(r.Name), tooltip: r => $"{MetadataTokens.GetHeapOffset(r.Name):X} \"{m.GetString(r.Name)}\"");
                AddGuid(columns, "HashAlgorithm", r => r.HashAlgorithm, r =>
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
                AddBlob(columns, "Hash", r => r.Hash, r => Hex(r.Hash));
                AddGuid(columns, "Language", r => r.Language, r =>
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

            TableIndex.MethodDebugInformation => AddColumns(Table(() => m.MethodDebugInformation.Select(m.GetMethodDebugInformation)), columns =>
            {
                AddHandle(columns, "Document", r => r.Document, r =>
                {
                    if (r.Document.IsNil)
                    {
                        return null;
                    }

                    var document = m.GetDocument(r.Document);
                    return $"{MetadataTokens.GetHeapOffset(document.Name):X} \"{m.GetString(document.Name)}\"";
                });
                AddBlob(columns, "SequencePoints", r => r.SequencePointsBlob, r =>
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
                AddHandle(columns, "LocalSignature", r => r.LocalSignature, r =>
                {
                    if (r.LocalSignature.IsNil)
                    {
                        return null;
                    }

                    return LocalsText(file, m.GetStandaloneSignature(r.LocalSignature));
                });
            }),

            TableIndex.LocalScope => AddColumns(Table(() => m.LocalScopes.Select(m.GetLocalScope)), columns =>
            {
                AddHandle(columns, "Method", r => r.Method);
                AddHandle(columns, "ImportScope", r => r.ImportScope, NoTooltip);
                AddHandle(columns, "VariableList", r => r.GetLocalVariables().FirstOrDefault(), NoTooltip);
                AddHandle(columns, "ConstantList", r => r.GetLocalConstants().FirstOrDefault(), NoTooltip);
                AddValue(columns, "StartOffset", r => r.StartOffset);
                AddValue(columns, "Length", r => r.Length);
            }),

            TableIndex.LocalVariable => AddColumns(Table(() => m.LocalVariables.Select(m.GetLocalVariable)), columns =>
            {
                AddValue(columns, "Attributes", r => r.Attributes, "X8");
                AddValue(columns, "Index", r => r.Index);
                AddName(columns, r => r.Name);
            }),

            TableIndex.LocalConstant => AddColumns(Table(() => m.LocalConstants.Select(m.GetLocalConstant)), columns =>
            {
                AddName(columns, r => r.Name);
                AddBlob(columns, "Signature", r => r.Signature);
            }),

            TableIndex.ImportScope => AddColumns(Table(() => m.ImportScopes.Select(m.GetImportScope)), columns =>
            {
                AddHandle(columns, "Parent", r => r.Parent, NoTooltip);
                AddBlob(columns, "Imports", r => r.ImportsBlob);
            }),

            TableIndex.StateMachineMethod => AddColumns(Table(() => StateMachineMethods(m)), columns =>
            {
                AddHandle(columns, "MoveNextMethod", r => r.MoveNext);
                AddHandle(columns, "KickoffMethod", r => r.Kickoff);
            }),

            TableIndex.CustomDebugInformation => CustomDebugInformationTable.Columns(Table(() => m.CustomDebugInformation.Select(m.GetCustomDebugInformation)), file),

            TableIndex.EncLog => AddColumns(Table(m.GetEditAndContinueLogEntries), columns =>
            {
                AddOffset(columns, table);
                AddHandle(columns, "Handle", r => r.Handle);
                AddValue(columns, "FuncCode", r => r.Operation, "X8", r => r.Operation.ToString());
            }),

            TableIndex.EncMap => AddColumns(Table(m.GetEditAndContinueMapEntries), columns =>
            {
                AddOffset(columns, table);
                AddHandle(columns, "Handle", r => r);
            }),

            TableIndex.FieldPtr or TableIndex.MethodPtr or TableIndex.ParamPtr or TableIndex.EventPtr or TableIndex.PropertyPtr => AddColumns(Table(() => m.GetPtrRows(table)), columns =>
            {
                AddHandle(columns, "Handle", r => r);
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

    /// <summary>The literal <paramref name="constant"/> holds, as C# writes it; null when its blob cannot be read.</summary>
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

    /// <summary>The locals a signature of local variables declares, a line each as <c>[index] type</c>; null
    /// for a signature of another kind, or one that cannot be read.</summary>
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

    /// <summary>A property's signature as IL declares it: <c>instance</c> unless static, its type, its name
    /// and the types of its index parameters, e.g. <c>instance string Item(int32)</c>; null when it cannot be read.</summary>
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

    /// <summary>The type that declares <paramref name="property"/>, found through an accessor, so that its
    /// generic parameters are written by name; null for a property without accessors.</summary>
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

    /// <summary>The StateMachineMethod rows, read from the table: System.Reflection.Metadata has no
    /// reader for them. A MethodDef index is two bytes or four, as half the row is.</summary>
    private static IEnumerable<(MethodDefinitionHandle MoveNext, MethodDefinitionHandle Kickoff)> StateMachineMethods(MetadataReader metadata)
    {
        var length = metadata.GetTableRowCount(TableIndex.StateMachineMethod);
        var reader = metadata.AsBlobReader();
        reader.Offset = metadata.GetTableMetadataOffset(TableIndex.StateMachineMethod);
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

    /// <summary>The grid shown, made the first time it is asked for; null for a part shown as text.</summary>
    internal static MetadataGrid? EnsureGrid(MetadataTableInfo table)
    {
        return table.Part.Match<MetadataGrid?>(
            static grid => grid.View.Value,
            static directory => directory.View.Value,
            static _ => null,
            static _ => null,
            static _ => null);
    }

    internal static MetadataTableInfo CreateGridPart(LoadedModule module, MetadataFile file, string title, MetadataPartKey key, MetadataGridRole role,
        GridSchema schema)
    {
        return new MetadataTableInfo { Module = module, File = file, Title = title, Key = key, Part = new MetadataPart.Grid(role, schema, new Lazy<MetadataGrid>(() => MetadataGrids.CreateGrid(title, module, file, schema))) };
    }

    internal static MetadataTableInfo CreateDebugDirectoryPart(LoadedModule module, PEFile file)
    {
        const string title = "Debug Directory";
        var schema = PEHeaderEntries.DebugDirectory(file);
        return new MetadataTableInfo { Module = module, File = file, Title = title, Key = new MetadataPartKey.DebugDirectory(), Part = new MetadataPart.DebugDirectory(file, new Lazy<MetadataGrid>(() => MetadataGrids.CreateGrid(title, module, file, schema))) };
    }

    internal static MetadataTableInfo CreateRowCountPart(LoadedModule module, MetadataFile file, TableIndex table, int rows)
    {
        return new MetadataTableInfo { Module = module, File = file, Title = $"{(byte)table:X2} {table} ({rows})", Key = new MetadataPartKey.Table(table), Part = new MetadataPart.RowCount(table, rows) };
    }

    internal static MetadataTableInfo CreateDecodedEntryPart(LoadedModule module, PEFile file, int index, DebugDirectoryEntry entry, IReadOnlyList<string> lines)
    {
        return new MetadataTableInfo { Module = module, File = file, Title = entry.Type.ToString(), Key = new MetadataPartKey.DebugEntry(index, entry.Type), Part = new MetadataPart.DecodedDebugEntry(lines) };
    }

    internal static MetadataTableInfo CreateRawEntryPart(LoadedModule module, PEFile file, int index, DebugDirectoryEntry entry)
    {
        return new MetadataTableInfo { Module = module, File = file, Title = entry.Type.ToString(), Key = new MetadataPartKey.DebugEntry(index, entry.Type), Part = new MetadataPart.RawDebugEntry(file, entry) };
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

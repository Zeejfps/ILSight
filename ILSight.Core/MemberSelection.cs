// Adapted from ILSpy's CSharpLanguage (MIT License, Copyright (c) AlphaSierraPapa for the SharpDevelop Team).

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>
/// How ILSpy shows a constructor or a field on its own: the type's fields, events, properties and
/// constructors of the same staticness are decompiled together, so initializers moved into a
/// constructor come back out; <see cref="ConstructorSelection"/> and <see cref="FieldSelection"/> then remove
/// everything but the one asked for.
/// </summary>
internal static class MemberSelection
{
    public static List<EntityHandle> FieldsAndConstructors(ITypeDefinition type, bool isStatic)
    {
        var members = new List<EntityHandle>();
        foreach (var field in type.Fields)
        {
            if (!field.MetadataToken.IsNil && field.IsStatic == isStatic)
            {
                members.Add(field.MetadataToken);
            }
        }
        foreach (var e in type.Events)
        {
            if (!e.MetadataToken.IsNil && e.IsStatic == isStatic)
            {
                members.Add(e.MetadataToken);
            }
        }
        foreach (var p in type.Properties)
        {
            if (!p.MetadataToken.IsNil && p.IsStatic == isStatic && !p.IsIndexer)
            {
                members.Add(p.MetadataToken);
            }
        }
        foreach (var ctor in type.Methods)
        {
            if (!ctor.MetadataToken.IsNil && ctor.IsConstructor && ctor.IsStatic == isStatic)
            {
                members.Add(ctor.MetadataToken);
            }
        }
        return members;
    }
}

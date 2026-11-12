namespace ILSight.Core;

/// <summary>
/// A parsed search box query, with ILSpy's syntax. Whitespace separates terms; double quotes keep
/// one together. A term matches a name containing it, ignoring case; every term must match.
/// <list type="bullet">
/// <item><c>-term</c> must not be contained, <c>=term</c> must be the whole name, <c>~term</c>
/// matches letters in order with gaps, <c>+term</c> is a plain term.</item>
/// <item><c>/regex/</c> matches a regular expression instead, ignoring case. The first one
/// counts; when it is invalid nothing is found, and <see cref="Matcher"/> says why.</item>
/// <item>A term with a dot matches qualified names, e.g. <c>Generic.List</c>.</item>
/// <item>A term with <c>&lt;</c> matches names with their type parameters, C#-style, e.g.
/// <c>List&lt;T&gt;</c>.</item>
/// <item>Kind prefixes replace the <see cref="Kinds"/> picked, and add up when there are several:
/// <c>tm:</c> <c>t:</c> <c>m:</c> <c>md:</c> <c>f:</c> <c>p:</c> <c>e:</c> <c>c:</c> (literals)
/// <c>r:</c> <c>n:</c>, <c>a:</c> / <c>af:</c> / <c>an:</c> (assembly by name, file path, full
/// name), <c>@</c> (metadata token).</item>
/// <item><c>inassembly:</c> and <c>innamespace:</c> keep results to assemblies or namespaces
/// whose name contains the value.</item>
/// </list>
/// </summary>
public sealed class SearchQuery
{
    public required SearchKinds Kinds { get; set; }

    public AssemblyNameKind AssemblyName { get; internal set; }

    /// <summary>How names are matched: by the terms, or by the regular expression, or not at all
    /// when the expression is invalid.</summary>
    public SearchMatcher Matcher { get; internal set; } = new SearchMatcher.Terms { Values = [] };

    /// <summary>Whether names are matched qualified, namespace and declaring types included.</summary>
    public bool FullName { get; internal set; }

    /// <summary>Whether a term names generic arity, so names keep their <c>`1</c> suffix.</summary>
    public bool KeepArity { get; internal set; }

    /// <summary>Whether a term names type parameters, so generic names are written with them, e.g.
    /// <c>Dictionary&lt;TKey, TValue&gt;</c>.</summary>
    public bool Generics { get; internal set; }

    public string? InAssembly { get; internal set; }

    public string? InNamespace { get; internal set; }

    /// <summary>Whether the query can find anything: it has terms, or a valid regular expression.</summary>
    public bool CanFind => Matcher.Match(static terms => terms.Values.Count > 0, static _ => true, static _ => false);
}

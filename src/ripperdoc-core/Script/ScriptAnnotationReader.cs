using System.Text.RegularExpressions;

namespace Ripperdoc.Core.Script;

/// <summary>
/// Reads the contending annotations out of one script source.
/// </summary>
/// <remarks>
/// Annotation level, not language level. What this recognises is an annotation,
/// the name of the function declared beneath it, and whether that function's
/// body calls the wrapped method - three lexical facts. It does not resolve
/// types, does not follow imports, and does not know what any expression means.
/// <para>
/// The one thing it does before matching is blank comments and strings
/// (<see cref="ScriptText" />), because those are where text alone would
/// otherwise find an annotation that is not one.
/// </para>
/// </remarks>
public static class ScriptAnnotationReader
{
    private static readonly Regex AnnotationPattern = new(
        @"@(?<kind>replaceMethod|wrapMethod)\s*\(\s*(?<type>[A-Za-z_][A-Za-z0-9_]*)\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FunctionPattern = new(
        @"\bfunc\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WrappedCallPattern = new(
        @"\bwrappedMethod\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Reads <paramref name="text" /> as the contents of <paramref name="source" />.
    /// </summary>
    public static ScriptFileReading Read(ScriptSource source, string text)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(text);

        var blanked = ScriptText.Blanked(text);
        var found = new List<ScriptAnnotation>();
        var unattached = new List<int>();

        var matches = AnnotationPattern.Matches(blanked);
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];

            // The declaration is looked for only as far as the next annotation.
            // Without that bound an annotation with nothing beneath it would
            // adopt the function belonging to the annotation after it, and
            // report a conflict on a method nobody wrote one for.
            var limit = i + 1 < matches.Count ? matches[i + 1].Index : blanked.Length;
            var declaration = FunctionPattern.Match(blanked, match.Index + match.Length, limit - (match.Index + match.Length));

            if (!declaration.Success)
            {
                unattached.Add(ScriptText.LineAt(blanked, match.Index));
                continue;
            }

            var kind = blanked[match.Groups["kind"].Index] == 'r'
                ? ScriptAnnotationKind.ReplaceMethod
                : ScriptAnnotationKind.WrapMethod;

            var callsWrapped = false;
            if (kind == ScriptAnnotationKind.WrapMethod)
            {
                var bodyEnd = ScriptText.EndOfBody(blanked, declaration.Index);
                if (bodyEnd > declaration.Index)
                {
                    callsWrapped = WrappedCallPattern.IsMatch(
                        blanked[declaration.Index..bodyEnd]);
                }
            }

            found.Add(new ScriptAnnotation(
                kind,
                new MethodIdentity(match.Groups["type"].Value, declaration.Groups["name"].Value),
                source,
                ScriptText.LineAt(blanked, match.Index),
                callsWrapped));
        }

        return new ScriptFileReading(source, found, unattached);
    }
}

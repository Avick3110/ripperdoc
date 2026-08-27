using System.Text.RegularExpressions;

namespace Ripperdoc.Core.Script;

/// <summary>
/// Reads the contending annotations out of one script source.
/// </summary>
/// <remarks>
/// Annotation level, not language level. What this recognises is an annotation,
/// the name of the function declared beneath it, whether a conditional-
/// compilation gate stands above it, and whether that function's body calls the
/// wrapped method - four lexical facts. It does not resolve types, does not
/// follow imports, and does not know what any expression means.
/// <para>
/// The one thing it does before matching is blank comments and strings
/// (<see cref="ScriptText" />), because those are where text alone would
/// otherwise find an annotation that is not one.
/// </para>
/// <para>
/// It reads a gate and does not evaluate one. A false gate removes the
/// declaration beneath it from the compile entirely and a true gate leaves it
/// untouched, both measured; which of the two a given gate is rests on a rule
/// this project has not measured, so what a gate produces here is a marker and
/// never a decision.
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

    private static readonly Regex GatePattern = new(
        @"@if\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Reads <paramref name="text" /> as the contents of <paramref name="source" />.
    /// </summary>
    public static ScriptFileReading Read(ScriptSource source, string text)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(text);

        var blanked = ScriptText.Blanked(text);
        var gateEnds = GateEnds(blanked);
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

            found.Add(new ScriptAnnotation(
                kind,
                new MethodIdentity(match.Groups["type"].Value, declaration.Groups["name"].Value),
                source,
                ScriptText.LineAt(blanked, match.Index),
                ReadWrappedCall(kind, blanked, declaration.Index),
                IsGated(blanked, gateEnds, match.Index)));
        }

        return new ScriptFileReading(source, found, unattached);
    }

    private static WrappedCallReading ReadWrappedCall(ScriptAnnotationKind kind, string blanked, int declarationIndex)
    {
        if (kind != ScriptAnnotationKind.WrapMethod)
        {
            return WrappedCallReading.NotAWrap;
        }

        var bodyEnd = ScriptText.EndOfBody(blanked, declarationIndex);
        if (bodyEnd <= declarationIndex)
        {
            return WrappedCallReading.BodyNotResolved;
        }

        return WrappedCallPattern.IsMatch(blanked[declarationIndex..bodyEnd])
            ? WrappedCallReading.Calls
            : WrappedCallReading.DoesNotCall;
    }

    /// <summary>
    /// The index just past each conditional-compilation gate in the source, in
    /// order.
    /// </summary>
    private static List<int> GateEnds(string blanked)
    {
        var ends = new List<int>();
        foreach (Match gate in GatePattern.Matches(blanked))
        {
            var end = ScriptText.EndOfParenthesised(blanked, gate.Index + gate.Length - 1);
            if (end > 0)
            {
                ends.Add(end);
            }
        }

        return ends;
    }

    /// <summary>
    /// Whether a gate stands immediately above the annotation at
    /// <paramref name="annotationIndex" />.
    /// </summary>
    /// <remarks>
    /// A gate reaches exactly the one declaration that follows it, measured, so
    /// only the nearest gate above the annotation can be its own - and it is its
    /// own only when nothing but whitespace lies between. Comments are already
    /// blanked to spaces when this runs, which is why a comment between the two
    /// does not break the pairing, as measured.
    /// </remarks>
    private static bool IsGated(string blanked, List<int> gateEnds, int annotationIndex)
    {
        var nearest = -1;
        foreach (var end in gateEnds)
        {
            if (end <= annotationIndex && end > nearest)
            {
                nearest = end;
            }
        }

        if (nearest < 0)
        {
            return false;
        }

        for (var i = nearest; i < annotationIndex; i++)
        {
            if (!char.IsWhiteSpace(blanked[i]))
            {
                return false;
            }
        }

        return true;
    }
}

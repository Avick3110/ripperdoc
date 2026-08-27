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
    /// Any annotation at all, including the ones this engine does not resolve.
    /// </summary>
    /// <remarks>
    /// The bound on the search for a declaration. Bounding at the next
    /// <em>contending</em> annotation leaves an annotation with nothing beneath
    /// it free to adopt the function belonging to an <c>@addMethod</c>, and
    /// report it as a live replacement of a method nobody wrote one for. The
    /// annotations this engine does not resolve are the common neighbour on a
    /// real layer, not a curiosity.
    /// </remarks>
    private static readonly Regex AnyAnnotationPattern = new(
        @"@(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// The annotations this engine contends over, by name alone.
    /// </summary>
    /// <remarks>
    /// Read from the bounding pattern's own match rather than from
    /// <see cref="AnnotationPattern" />, so that an occurrence carrying an
    /// argument shape that pattern does not model is still known to be one of
    /// these. Which argument shapes the language allows is not decided here.
    /// </remarks>
    private static readonly string[] ContendingNames = ["replaceMethod", "wrapMethod"];

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

        var anyAnnotations = AnyAnnotationPattern.Matches(blanked);
        var annotationStarts = anyAnnotations.Select(m => m.Index).ToList();

        var matches = AnnotationPattern.Matches(blanked);
        var resolvedStarts = matches.Select(m => m.Index).ToHashSet();

        // An occurrence this engine contends over that the argument pattern did
        // not match is recorded rather than passed over. It is recognised well
        // enough to bound the search above it either way, so passing over it
        // keeps the cost of not modelling the argument and loses the report of
        // it - which is the carrier going missing with nothing said.
        foreach (Match any in anyAnnotations)
        {
            if (ContendingNames.Contains(any.Groups["name"].Value) && !resolvedStarts.Contains(any.Index))
            {
                unattached.Add(ScriptText.LineAt(blanked, any.Index));
            }
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];

            // The declaration is looked for only as far as the next annotation
            // of any kind. Without that bound an annotation with nothing beneath
            // it adopts the function belonging to whatever follows, and reports
            // a conflict on a method nobody wrote one for.
            var from = match.Index + match.Length;
            var limit = NextAnnotationAfter(annotationStarts, match.Index, blanked.Length);
            var declaration = limit > from
                ? FunctionPattern.Match(blanked, from, limit - from)
                : Match.Empty;

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

        // Two passes fill this list and they run in different orders, so it is
        // put back into the source's own order before it is reported.
        unattached.Sort();

        return new ScriptFileReading(source, found, unattached);
    }

    /// <summary>
    /// Where the search for a declaration has to stop: the start of the next
    /// annotation after <paramref name="index" />, or the end of the source.
    /// </summary>
    private static int NextAnnotationAfter(List<int> annotationStarts, int index, int end)
    {
        foreach (var start in annotationStarts)
        {
            if (start > index)
            {
                return start;
            }
        }

        return end;
    }

    private static WrappedCallReading ReadWrappedCall(ScriptAnnotationKind kind, string blanked, int declarationIndex)
    {
        if (kind != ScriptAnnotationKind.WrapMethod)
        {
            return WrappedCallReading.NotAWrap;
        }

        if (ScriptText.DeclaresNoBody(blanked, declarationIndex))
        {
            return WrappedCallReading.BodyNotResolved;
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

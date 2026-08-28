using Ripperdoc.Core.Reporting;

namespace Ripperdoc.Core.Script;

/// <summary>
/// A span of script source this engine's lexical pass models, with a source
/// that exercises it.
/// </summary>
/// <remarks>
/// <para>
/// The boundary of <see cref="ScriptText" />, written down where it can be
/// checked rather than described in prose beside it. Each member is a place an
/// annotation can appear without being one, or - for an interpolation - a place
/// ordinary code can appear inside one of those.
/// </para>
/// <para>
/// <strong>This set bounds what the model handles, not what the language
/// contains.</strong> A category nobody wrote down is absent from here as
/// surely as it is absent from the pass, and no derivation inside this
/// repository can find it: the set that would settle it is the compiler's own
/// grammar. What stands in its place is a measurement against the compiler,
/// which is open work rather than a check that runs - see issue 45. Until it
/// runs, agreement between this pass and the compiler rests on measurements
/// taken once.
/// </para>
/// <para>
/// The direction the pass fails in is what makes the gap survivable: an
/// unmodelled shape leaves an annotation unresolved or undetermined rather than
/// live, so what is lost is a report and never a wrong winner.
/// </para>
/// </remarks>
internal sealed class ScriptTextSpan
{
    /// <summary>Text from a line-comment marker to the end of its line.</summary>
    public static readonly ScriptTextSpan LineComment = new(
        "public func M() -> Void {} // " + AnAnnotation + "\n",
        blanked => !blanked.Contains(AnAnnotation, StringComparison.Ordinal));

    /// <summary>Text between the block-comment markers, across lines.</summary>
    public static readonly ScriptTextSpan BlockComment = new(
        "/*\n" + AnAnnotation + "\n*/\npublic func M() -> Void {}\n",
        blanked => !blanked.Contains(AnAnnotation, StringComparison.Ordinal));

    /// <summary>A string literal, whose contents are not code.</summary>
    public static readonly ScriptTextSpan StringLiteral = new(
        "public func M() -> Void {\n  Log(\"" + AnAnnotation + "\");\n}\n",
        blanked => !blanked.Contains(AnAnnotation, StringComparison.Ordinal));

    /// <summary>
    /// An interpolation inside a string literal, whose contents are code.
    /// </summary>
    /// <remarks>
    /// Both directions matter, and a model reading a literal as one flat span
    /// gets each of them wrong: the interpolation's own code is hidden, and a
    /// string opened inside it is handed back as live code - enough to read an
    /// annotation out of a message.
    /// </remarks>
    public static readonly ScriptTextSpan Interpolation = new(
        "public func M() -> Void {\n  Log(\"a \\(" + AMarker + "(\"" + AnAnnotation + "\")) b\");\n}\n",
        blanked => !blanked.Contains(AnAnnotation, StringComparison.Ordinal)
            && blanked.Contains(AMarker, StringComparison.Ordinal));

    private ScriptTextSpan(string source, Func<string, bool> holdsOfBlanked)
    {
        Source = source;
        HoldsOfBlanked = holdsOfBlanked;
        DeclaredKinds.Register(this);
    }

    /// <summary>Every span this pass models.</summary>
    internal static IReadOnlyList<KindMember<ScriptTextSpan>> All =>
        DeclaredKinds.Of<ScriptTextSpan>();

    /// <summary>A source carrying this span.</summary>
    internal string Source { get; }

    /// <summary>What the pass has to have done to <see cref="Source" />.</summary>
    internal Func<string, bool> HoldsOfBlanked { get; }

    // An annotation shape and an identifier the checks look for. The first must
    // never survive inside a span this pass blanks; the second must survive,
    // because interpolated code is code.
    private const string AnAnnotation = "@replaceMethod(PlayerPuppet)";
    private const string AMarker = "InterpolatedCallSurvives";
}

namespace Ripperdoc.Core.Script;

/// <summary>
/// Everything one method is subject to, resolved under the measured law.
/// </summary>
/// <remarks>
/// The law this implements, in one line: among the replacements of a method,
/// the <strong>last in compile order</strong> takes the body, and every wrap is
/// kept regardless of where it sits relative to that replacement.
/// <para>
/// Only annotations this engine resolved take part. An annotation carrying a
/// conditional-compilation gate is held out in <see cref="UndeterminedInCompileOrder" />
/// instead, because a gate decides whether its declaration is compiled at all
/// and this engine does not decide gates. Counting one in would be the inverted
/// answer the layer exists to prevent: a gated-out replacement would be named
/// the winner, and the replacement that actually takes the method would be
/// reported as doing nothing.
/// </para>
/// <para>
/// Nothing here assembles a sentence. Every fact a sentence would carry is a
/// member of this type, and a sentence naming a mod says more than its parts
/// as soon as one of them turns out not to hold - a result can be reported
/// uncontested and carry, in the same breath, a gated annotation that would
/// contest it. The order every annotation list on this type is given in is in
/// that list's name rather than in prose, because the order is the part a
/// caller cannot recover from the data, and the reading this project has not
/// measured is the one a reader supplies unaided.
/// </para>
/// </remarks>
public sealed class MethodContest
{
    internal MethodContest(
        MethodIdentity method,
        IReadOnlyList<ScriptAnnotation> replacements,
        IReadOnlyList<ScriptAnnotation> wraps,
        IReadOnlyList<ScriptAnnotation> undetermined,
        ScriptEnumeration enumeration,
        IReadOnlyList<ScriptFileReading> readings)
    {
        Method = method;
        ReplacementsInCompileOrder = replacements;
        WrapsInCompileOrder = wraps;
        UndeterminedInCompileOrder = undetermined;
        Enumeration = enumeration;
        Readings = readings;
    }

    /// <summary>The compile order this result was resolved against.</summary>
    /// <remarks>
    /// Carried on the result because a limit is a statement about the reading
    /// this result came out of, and some of them are properties of the whole
    /// reading rather than of one method.
    /// </remarks>
    internal ScriptEnumeration Enumeration { get; }

    /// <summary>Every source read.</summary>
    /// <remarks>
    /// No order is claimed. Nothing here reads one: the tests over this list ask
    /// whether any reading holds something, and a sequence stated and not held
    /// is the prose claim this type exists to stop making.
    /// </remarks>
    internal IReadOnlyList<ScriptFileReading> Readings { get; }

    /// <summary>The method these annotations target.</summary>
    public MethodIdentity Method { get; }

    /// <summary>Every resolved replacement of it, in compile order.</summary>
    public IReadOnlyList<ScriptAnnotation> ReplacementsInCompileOrder { get; }

    /// <summary>
    /// Every resolved wrap of it, <strong>in compile order</strong>.
    /// </summary>
    /// <remarks>
    /// <strong>This is the order the sources are compiled in, and nothing
    /// else.</strong> Which wrap encloses which at run time was not measured and
    /// is not claimed here: every wrap in a chain is emitted, and the emitted
    /// code is the same whichever way they compose, so the observation that
    /// settled the replacement law cannot see it at all. A reader taking this
    /// sequence for an execution order is reading in something this project has
    /// not established.
    /// </remarks>
    public IReadOnlyList<ScriptAnnotation> WrapsInCompileOrder { get; }

    /// <summary>
    /// Annotations on this method that carry a gate, and so are neither
    /// resolved as live nor known to be absent.
    /// </summary>
    public IReadOnlyList<ScriptAnnotation> UndeterminedInCompileOrder { get; }

    /// <summary>
    /// The replacement that takes the method, or <see langword="null" /> when
    /// nothing resolved replaces it.
    /// </summary>
    public ScriptAnnotation? Winner =>
        ReplacementsInCompileOrder.Count == 0 ? null : ReplacementsInCompileOrder[^1];

    /// <summary>
    /// The replacements that lose - every one but the last.
    /// </summary>
    /// <remarks>
    /// This is the state the layer is worth reporting for. A mod here installed
    /// cleanly, compiled cleanly, and does nothing at all for this method.
    /// </remarks>
    public IReadOnlyList<ScriptAnnotation> OverriddenInCompileOrder =>
        ReplacementsInCompileOrder.Count <= 1
            ? []
            : ReplacementsInCompileOrder.Take(ReplacementsInCompileOrder.Count - 1).ToList();

    /// <summary>
    /// The loser that no compiler diagnostic names.
    /// </summary>
    /// <remarks>
    /// The compiler warns once per replacement <em>after</em> the first, and
    /// attaches each warning to the replacement doing the overwriting. So in a
    /// contest the first replacement is the one loser that appears in no
    /// warning anywhere: reading the log start to finish never names it.
    /// </remarks>
    public ScriptAnnotation? LoserNoWarningNames =>
        ReplacementsInCompileOrder.Count <= 1 ? null : ReplacementsInCompileOrder[0];

    /// <summary>Whether more than one resolved source replaces this method.</summary>
    public bool IsContested => ReplacementsInCompileOrder.Count > 1;

    /// <summary>
    /// Every reason this result could be wrong.
    /// </summary>
    /// <remarks>
    /// Computed from the result rather than from whether it names a winner. A
    /// method reported as unreplaced is displaceable by exactly the inputs a
    /// replaced one is, so it carries the same limits.
    /// <para>
    /// Every declared limit is asked, rather than a remembered few being added.
    /// The limits belonging to the whole reading are not narrowed to the
    /// contests they touch, because which contests those are is the part that
    /// is unknown - each says so in its own test.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ScriptResolutionLimit> Limits =>
        ScriptResolutionLimit.All.Where(limit => limit.AppliesTo(this)).ToList();

    /// <summary>Whether anything about this result was left unresolved.</summary>
    public bool ResultIsProvisional => Limits.Count > 0;
}

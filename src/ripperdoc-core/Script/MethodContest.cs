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
/// conditional-compilation gate is held out in <see cref="Undetermined" />
/// instead, because a gate decides whether its declaration is compiled at all
/// and this engine does not decide gates. Counting one in would be the inverted
/// answer the layer exists to prevent: a gated-out replacement would be named
/// the winner, and the replacement that actually takes the method would be
/// reported as doing nothing.
/// </para>
/// </remarks>
public sealed class MethodContest
{
    private readonly PluginScriptPosture _posture;
    private readonly IReadOnlyList<ScriptResolutionLimit> _layerLimits;

    internal MethodContest(
        MethodIdentity method,
        IReadOnlyList<ScriptAnnotation> replacements,
        IReadOnlyList<ScriptAnnotation> wraps,
        IReadOnlyList<ScriptAnnotation> undetermined,
        PluginScriptPosture posture,
        IReadOnlyList<ScriptResolutionLimit> layerLimits)
    {
        Method = method;
        Replacements = replacements;
        Wraps = wraps;
        Undetermined = undetermined;
        _posture = posture;
        _layerLimits = layerLimits;
    }

    /// <summary>The method these annotations target.</summary>
    public MethodIdentity Method { get; }

    /// <summary>Every resolved replacement of it, in compile order.</summary>
    public IReadOnlyList<ScriptAnnotation> Replacements { get; }

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
    public IReadOnlyList<ScriptAnnotation> Wraps { get; }

    /// <summary>
    /// Annotations on this method that carry a gate, and so are neither
    /// resolved as live nor known to be absent.
    /// </summary>
    public IReadOnlyList<ScriptAnnotation> Undetermined { get; }

    /// <summary>
    /// The replacement that takes the method, or <see langword="null" /> when
    /// nothing resolved replaces it.
    /// </summary>
    public ScriptAnnotation? Winner =>
        Replacements.Count == 0 ? null : Replacements[^1];

    /// <summary>
    /// The replacements that lose - every one but the last.
    /// </summary>
    /// <remarks>
    /// This is the state the layer is worth reporting for. A mod here installed
    /// cleanly, compiled cleanly, and does nothing at all for this method.
    /// </remarks>
    public IReadOnlyList<ScriptAnnotation> Overridden =>
        Replacements.Count <= 1 ? [] : Replacements.Take(Replacements.Count - 1).ToList();

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
        Replacements.Count <= 1 ? null : Replacements[0];

    /// <summary>Whether more than one resolved source replaces this method.</summary>
    public bool IsContested => Replacements.Count > 1;

    /// <summary>
    /// Every reason this result could be wrong.
    /// </summary>
    /// <remarks>
    /// Computed from the result rather than from whether it names a winner. A
    /// method reported as unreplaced is displaceable by exactly the inputs a
    /// replaced one is, so it carries the same limits.
    /// </remarks>
    public IReadOnlyList<ScriptResolutionLimit> Limits
    {
        get
        {
            var limits = new List<ScriptResolutionLimit>();

            if (_posture == PluginScriptPosture.NotSupplied)
            {
                limits.Add(ScriptResolutionLimit.PluginScriptsNotSupplied);
            }

            if (Undetermined.Count > 0)
            {
                limits.Add(ScriptResolutionLimit.GatedAnnotationPresent);
            }

            if (Wraps.Any(annotation => annotation.BodyCouldNotBeRead))
            {
                limits.Add(ScriptResolutionLimit.WrapBodyNotResolved);
            }

            // Limits the whole reading carries. They are not narrowed to
            // the contests they touch, because which contests those are is
            // the part that is unknown.
            limits.AddRange(_layerLimits);

            return limits;
        }
    }

    /// <summary>Whether anything about this result was left unresolved.</summary>
    public bool ResultIsProvisional => Limits.Count > 0;

    /// <summary>
    /// One sentence saying what happens to this method and what is not known
    /// about it.
    /// </summary>
    public string Describe()
    {
        var parts = new List<string>();

        if (Winner is null)
        {
            parts.Add($"{Method.Display} is not replaced by any source this reading resolved");
        }
        else if (IsContested)
        {
            parts.Add(
                $"{Method.Display} is replaced by {Replacements.Count} sources, and "
                + $"{Winner.Source.Display} wins because it is last in compile order");
            parts.Add(
                $"{Overridden.Count} replacement(s) are overridden and do nothing, of which "
                + $"{LoserNoWarningNames!.Source.Display} is named by no compiler warning");
        }
        else
        {
            parts.Add($"{Method.Display} is replaced by {Winner.Source.Display}, uncontested");
        }

        if (Wraps.Count > 0)
        {
            parts.Add($"{Wraps.Count} wrap(s) are listed in compile order");
        }

        foreach (var limit in Limits)
        {
            parts.Add(Explain(limit));
        }

        return string.Join("; ", parts) + ".";
    }

    private string Explain(ScriptResolutionLimit limit) => limit switch
    {
        ScriptResolutionLimit.PluginScriptsNotSupplied =>
            "no runtime-extension plugin scripts were supplied, and those compile after every "
            + "source here, so a plugin replacing this method would take it from whatever this "
            + "result names",
        ScriptResolutionLimit.GatedAnnotationPresent =>
            $"{Undetermined.Count} annotation(s) on this method are behind a conditional-compilation "
            + "gate whose value this engine does not decide, so they are left out of this result and "
            + "any of them may in fact apply",
        ScriptResolutionLimit.WrapBodyNotResolved =>
            $"{Wraps.Count(annotation => annotation.BodyCouldNotBeRead)} wrap(s) here have a body "
            + "this engine could not read to the end, so whether they continue the chain is unknown "
            + "rather than answered",
        ScriptResolutionLimit.SourceTakenOnAnUnmeasuredRule =>
            "this reading took a source whose extension is spelled with a capital, which this engine "
            + "includes on its own choice rather than on a measured rule, and the compile set decides "
            + "every winner here",
        ScriptResolutionLimit.AnnotationCouldNotBeAttached =>
            "somewhere in this reading an annotation has no declaration beneath it, so it names no "
            + "method and this result cannot be shown to have seen every carrier",
        _ => throw new ArgumentOutOfRangeException(nameof(limit), limit, "unhandled resolution limit"),
    };
}

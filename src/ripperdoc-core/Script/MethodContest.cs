namespace Ripperdoc.Core.Script;

/// <summary>
/// Everything one method is subject to, resolved under the measured law.
/// </summary>
/// <remarks>
/// The law this implements, in one line: among the replacements of a method,
/// the <strong>last in compile order</strong> takes the body, and every wrap is
/// kept regardless of where it sits relative to that replacement.
/// </remarks>
public sealed class MethodContest
{
    private readonly PluginScriptPosture _posture;

    internal MethodContest(
        MethodIdentity method,
        IReadOnlyList<ScriptAnnotation> replacements,
        IReadOnlyList<ScriptAnnotation> wraps,
        PluginScriptPosture posture)
    {
        Method = method;
        Replacements = replacements;
        Wraps = wraps;
        _posture = posture;
    }

    /// <summary>The method these annotations target.</summary>
    public MethodIdentity Method { get; }

    /// <summary>Every replacement of it, in compile order.</summary>
    public IReadOnlyList<ScriptAnnotation> Replacements { get; }

    /// <summary>
    /// Every wrap of it, <strong>in compile order</strong>.
    /// </summary>
    /// <remarks>
    /// <strong>This is the order the sources are compiled in, and nothing
    /// else.</strong> Which wrap ends up outermost at run time was not measured
    /// and is not claimed here: every wrap in a chain is emitted, and the
    /// emitted code is the same under any nesting, so the observation that
    /// settled the replacement law cannot see nesting at all. A reader taking
    /// this sequence for an execution order is reading in something this project
    /// has not established.
    /// </remarks>
    public IReadOnlyList<ScriptAnnotation> Wraps { get; }

    /// <summary>
    /// The replacement that takes the method, or <see langword="null" /> when
    /// nothing replaces it.
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

    /// <summary>Whether more than one source replaces this method.</summary>
    public bool IsContested => Replacements.Count > 1;

    /// <summary>
    /// Whether a source this reading never saw could take the method from
    /// <see cref="Winner" />.
    /// </summary>
    /// <remarks>
    /// True when no plugin-contributed sources were supplied. Those sit after
    /// the entire script directory in compile order, so one of them replacing
    /// this method beats everything named here.
    /// </remarks>
    public bool WinnerIsProvisional =>
        Winner is not null && _posture == PluginScriptPosture.NotSupplied;

    /// <summary>
    /// One sentence saying what happens to this method and what is not known
    /// about it.
    /// </summary>
    public string Describe()
    {
        var parts = new List<string>();

        if (Winner is null)
        {
            parts.Add($"{Method.Display} is not replaced");
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
            // Stated as what the order IS, not as a denial of what it is not.
            // A disclaimer has to name the stronger reading to deny it, and the
            // named reading is what survives a skim.
            parts.Add(
                $"{Wraps.Count} wrap(s) are listed in compile order, which is the order they are "
                + "compiled in and not an execution order");
        }

        if (WinnerIsProvisional)
        {
            parts.Add(
                "no runtime-extension plugin scripts were supplied, and those compile after every "
                + "source here, so a plugin replacing this method would take it from the winner named");
        }

        return string.Join("; ", parts) + ".";
    }
}

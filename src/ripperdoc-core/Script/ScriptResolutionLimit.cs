using Ripperdoc.Core.Reporting;

namespace Ripperdoc.Core.Script;

/// <summary>
/// A reason a resolved result could be wrong, carried on the result itself.
/// </summary>
/// <remarks>
/// <para>
/// Each of these is something the engine knows it did not resolve. They are
/// carried rather than discarded because the alternative is a result that reads
/// as settled while resting on something unread - and this layer's whole reason
/// to exist is that a mod can lose with nothing said about it.
/// </para>
/// <para>
/// A limit attaches to the result, not to a winner. A result that names no
/// winner is a claim too, and it is displaceable by the same unread input as one
/// that does.
/// </para>
/// <para>
/// A member is declared here with the test it applies under, the sentence it
/// contributes, and a layer it arises from - all three at one site, so that a
/// member cannot be declared and left unwired. The set every result is built
/// from is read back from these declarations
/// (<see cref="DeclaredKinds" />) rather than assembled beside them.
/// </para>
/// <para>
/// The set is ordered by name. Nothing reads meaning from the sequence, and a
/// reported order has to be the same on every run.
/// </para>
/// </remarks>
public sealed class ScriptResolutionLimit : IWitnessedKind
{
    /// <summary>
    /// The reading was not given the scripts runtime-extension plugins
    /// contribute, and those compile after every source it did read.
    /// </summary>
    public static readonly ScriptResolutionLimit PluginScriptsNotSupplied = new(
        contest => contest.Enumeration.PluginPosture == PluginScriptPosture.NotSupplied,
        "no runtime-extension plugin scripts were supplied, and those compile after every source "
            + "here, so a plugin replacing this method would take it from whatever this result names",
        new ScriptLayerWitness(("a.reds", AWrap)));

    /// <summary>
    /// An annotation on this method carries a conditional-compilation gate,
    /// whose value this engine does not decide.
    /// </summary>
    /// <remarks>
    /// A false gate removes the declaration beneath it from the compile
    /// entirely - no code, no contest, no warning - and a true gate leaves it
    /// exactly as though the gate were absent, both measured. Which of the two a
    /// given gate is depends on a rule nothing here has measured, so a gated
    /// annotation is kept out of the contest and named instead.
    /// </remarks>
    public static readonly ScriptResolutionLimit GatedAnnotationPresent = new(
        contest => contest.UndeterminedInCompileOrder.Count > 0,
        "annotations on this method are behind a conditional-compilation gate whose value this "
            + "engine does not decide, so they are left out of this result and any of them may in "
            + "fact apply",
        new ScriptLayerWitness(("a.reds", AGate + AReplacement)));

    /// <summary>
    /// A wrap on this method has a body this engine could not read to the end.
    /// </summary>
    /// <remarks>
    /// Whether that wrap continues the chain is then unknown, which is a
    /// different thing from knowing it does not.
    /// </remarks>
    public static readonly ScriptResolutionLimit WrapBodyNotResolved = new(
        contest => contest.WrapsInCompileOrder.Any(annotation => annotation.BodyCouldNotBeRead),
        "wraps here have a body this engine could not read to the end, so whether they continue "
            + "the chain is unknown rather than answered",
        new ScriptLayerWitness(("a.reds", AWrapWhoseBodyNeverCloses)));

    /// <summary>
    /// The compile order this result rests on includes a source taken on a rule
    /// nobody measured.
    /// </summary>
    /// <remarks>
    /// Whether the compiler reads a source whose extension is spelled with a
    /// capital was never observed; this engine takes them, because the file
    /// system it reads them from does not distinguish the spellings. One such
    /// source anywhere changes the compile set, and the compile set decides
    /// every winner, so this attaches to every result of the reading rather
    /// than to the contests the source happens to touch.
    /// </remarks>
    public static readonly ScriptResolutionLimit SourceTakenOnAnUnmeasuredRule = new(
        contest => contest.Enumeration.SourcesNotSpelledInLowerCase.Count > 0,
        "this reading took a source whose extension is spelled with a capital, which this engine "
            + "includes on its own choice rather than on a measured rule, and the compile set decides "
            + "every winner here",
        new ScriptLayerWitness(("a.RedS", AWrap)));

    /// <summary>
    /// Somewhere in this reading an annotation this engine contends over could
    /// not be resolved to a method.
    /// </summary>
    /// <remarks>
    /// Layer-wide, and deliberately not narrowed: such an annotation carries no
    /// method name, so which contest it would have joined is exactly what is
    /// unknown about it. Narrowing this to the methods it "probably" affects
    /// would be the guess the state exists to avoid. Either no declaration
    /// stands beneath it or its argument is a shape this engine does not model;
    /// the reported line is where a reader settles which.
    /// </remarks>
    public static readonly ScriptResolutionLimit AnnotationCouldNotBeAttached = new(
        contest => contest.AnnotationUnresolvedSomewhereInTheReading.Value,
        "somewhere in this reading an annotation could not be resolved to a method, so it names no "
            + "method and this result cannot be shown to have seen every carrier",
        new ScriptLayerWitness(("a.reds", AWrap), ("b.reds", AnAnnotationOverNothing)));

    private static readonly Lazy<IReadOnlyList<KindMember<ScriptResolutionLimit>>> Members =
        new(DeclaredKinds.Of<ScriptResolutionLimit>);

    private readonly Func<MethodContest, bool> _applies;
    private readonly ScriptLayerWitness _witness;

    private ScriptResolutionLimit(
        Func<MethodContest, bool> applies,
        string consequence,
        ScriptLayerWitness witness)
    {
        _applies = applies;
        Consequence = consequence;
        _witness = witness;
        DeclaredKinds.Register(this);
    }

    /// <summary>What this limit means for the result carrying it.</summary>
    /// <remarks>
    /// Invariant: it names no mod, no method and no count. Those belong to the
    /// result rather than to the limit, and a caller that wants them has them
    /// already - so the engine states what is unresolved and leaves the
    /// sentence to whoever knows their reader.
    /// <para>
    /// One third of that is checked: a consequence carries no digit, which is
    /// the count. The other two thirds are not, and there is no list of mod or
    /// method names to check them against. What stands behind those is that a
    /// limit is constructed with no access to a result, so it has nothing to
    /// name - a weaker guarantee than a check, and named as one.
    /// </para>
    /// </remarks>
    public string Consequence { get; }

    /// <summary>Every limit this engine can report.</summary>
    public static IReadOnlyList<ScriptResolutionLimit> All =>
        Members.Value.Select(member => member.Kind).ToList();

    /// <summary>The name this limit is declared under.</summary>
    public string Name
    {
        get
        {
            foreach (var member in Members.Value)
            {
                if (ReferenceEquals(member.Kind, this))
                {
                    return member.Name;
                }
            }

            throw new InvalidOperationException(
                "This limit is not among the ones read back from the declarations. Every limit a "
                + "result carries has to be one a check can find, or the completeness check passes "
                + "over it while results go on reporting it.");
        }
    }

    /// <inheritdoc />
    public override string ToString() => Name;

    ScriptLayerWitness IWitnessedKind.Witness => _witness;

    bool IWitnessedKind.AppliesTo(MethodContest contest) => AppliesTo(contest);

    internal bool AppliesTo(MethodContest contest) => _applies(contest);

    // The witness sources. A method that is wrapped or replaced is what makes a
    // contest exist at all, so every witness carries one - a layer that
    // resolves nothing reports nothing, and a limit reaches its result through
    // a result.
    private const string AType = "PlayerPuppet";
    private const string AMethod = "OnGameAttached";

    private const string AWrap =
        "@wrapMethod(" + AType + ")\npublic func " + AMethod
        + "() -> String {\n  return \"x\" + wrappedMethod();\n}\n";

    private const string AReplacement =
        "@replaceMethod(" + AType + ")\npublic func " + AMethod
        + "() -> String {\n  return \"x\";\n}\n";

    // The condition's text is arbitrary on purpose. This engine reads that a
    // gate is there and never what it evaluates to, so a witness picking a
    // condition meant to be true or false would rest on the thing the engine
    // refuses to decide.
    private const string AGate = "@if(ModuleExists(\"SomeOtherMod\"))\n";

    private const string AWrapWhoseBodyNeverCloses =
        "@wrapMethod(" + AType + ")\npublic func " + AMethod
        + "() -> String {\n  return \"x\" + wrappedMethod();\n";

    private const string AnAnnotationOverNothing = "@replaceMethod(" + AType + ")\n";
}

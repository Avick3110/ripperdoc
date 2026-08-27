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
/// </remarks>
public enum ScriptResolutionLimit
{
    /// <summary>
    /// The reading was not given the scripts runtime-extension plugins
    /// contribute, and those compile after every source it did read.
    /// </summary>
    PluginScriptsNotSupplied,

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
    GatedAnnotationPresent,

    /// <summary>
    /// A wrap on this method has a body this engine could not read to the end.
    /// </summary>
    /// <remarks>
    /// Whether that wrap continues the chain is then unknown, which is a
    /// different thing from knowing it does not.
    /// </remarks>
    WrapBodyNotResolved,

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
    SourceTakenOnAnUnmeasuredRule,

    /// <summary>
    /// Somewhere in this reading an annotation could not be attached to a
    /// declaration.
    /// </summary>
    /// <remarks>
    /// Layer-wide, and deliberately not narrowed: an annotation with no
    /// declaration beneath it has no method name, so which contest it would
    /// have joined is exactly what is unknown about it. Narrowing this to the
    /// methods it "probably" affects would be the guess the state exists to
    /// avoid.
    /// </remarks>
    AnnotationCouldNotBeAttached,
}

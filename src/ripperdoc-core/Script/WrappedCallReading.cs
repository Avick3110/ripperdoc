namespace Ripperdoc.Core.Script;

/// <summary>
/// What this engine could read about a wrap's call to the method it wraps.
/// </summary>
/// <remarks>
/// Three states and not two. A wrap that does not call what it wraps ends the
/// chain, and saying so names a mod - so the case where the body could not be
/// read to its end is kept apart from the case where it was read and held no
/// call. Folding the two would turn a failure to read into an accusation.
/// </remarks>
public enum WrappedCallReading
{
    /// <summary>
    /// The annotation is a replacement, which has nothing beneath it to call.
    /// </summary>
    NotAWrap,

    /// <summary>The body was read, and it calls the method it wraps.</summary>
    Calls,

    /// <summary>The body was read, and it holds no such call.</summary>
    DoesNotCall,

    /// <summary>
    /// The body's braces did not close, so this engine never found its end.
    /// </summary>
    /// <remarks>
    /// A source in this state would not compile either. It is reported rather
    /// than guessed at, because both guesses name someone.
    /// </remarks>
    BodyNotResolved,
}

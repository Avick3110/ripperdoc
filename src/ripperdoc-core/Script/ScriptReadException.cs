namespace Ripperdoc.Core.Script;

/// <summary>
/// A read of the script layer that could not be completed.
/// </summary>
/// <remarks>
/// The layer is plain text on disk and this engine does not classify the ways a
/// file system can refuse it. What it does do is refuse to report a partial
/// answer as a whole one: the compile order decides winners, and an order
/// missing sources names winners that are wrong with nothing said.
/// </remarks>
public sealed class ScriptReadException : Exception
{
    /// <summary>Builds the failure with the sentence a reader gets.</summary>
    /// <param name="message">What could not be read, and why no partial answer follows.</param>
    /// <param name="inner">The underlying error, carried as evidence.</param>
    public ScriptReadException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

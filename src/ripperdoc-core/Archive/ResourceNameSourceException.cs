namespace Ripperdoc.Core.Archive;

/// <summary>
/// A resource name source could not make its names available.
/// </summary>
/// <remarks>
/// Thrown rather than swallowed. A naming source that fails to load and says
/// nothing produces an inventory that is honest entry by entry - every entry is
/// still reported - and dishonest as a whole, because its provenance claims a
/// coverage it did not have. The two are not the same failure, and only the
/// second one is silent.
/// </remarks>
public sealed class ResourceNameSourceException : Exception
{
    /// <summary>Creates the exception with a message saying what to try next.</summary>
    public ResourceNameSourceException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception from an underlying failure.</summary>
    public ResourceNameSourceException(string message, Exception inner) : base(message, inner)
    {
    }
}

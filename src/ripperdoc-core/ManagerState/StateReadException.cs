namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// Something in the manager's on-disk state is not what this reader models.
/// </summary>
/// <remarks>
/// Every construct outside the modelled subset raises this rather than being
/// decoded on a guess. A reader that best-effort decodes an unknown version
/// marker, an unknown compression byte or a record whose checksum disagrees is
/// a reader whose answer is indistinguishable from a correct one, and the
/// wanted set is the input every later judgement rests on.
/// </remarks>
public sealed class StateReadException : Exception
{
    /// <summary>Creates the exception.</summary>
    public StateReadException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What was not modelled, and what to try next.</param>
    public StateReadException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What was not modelled, and what to try next.</param>
    /// <param name="innerException">What stopped the read.</param>
    public StateReadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// Something the diagnosis lane had to read could not be read.
/// </summary>
/// <remarks>
/// Thrown rather than absorbed into a partial reading. A diagnosis assembled
/// from an input that half-parsed reports mods as missing on the strength of a
/// file this engine did not understand, which is the silent wrong answer the
/// whole lane exists to avoid.
/// </remarks>
public sealed class DiagnosisReadException : Exception
{
    /// <summary>Creates the exception.</summary>
    public DiagnosisReadException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What could not be read, and what follows from it.</param>
    public DiagnosisReadException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What could not be read, and what follows from it.</param>
    /// <param name="innerException">What stopped the read.</param>
    public DiagnosisReadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

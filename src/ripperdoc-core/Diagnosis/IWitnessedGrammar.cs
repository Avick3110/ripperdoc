namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// A timestamp shape that carries a line it is required to read.
/// </summary>
/// <remarks>
/// An interface rather than two members on one type, for the same reason the
/// script layer's is: the completeness check has to be able to run over a set
/// that is deliberately wrong, or it is a check nobody has seen fail.
/// </remarks>
internal interface IWitnessedGrammar
{
    /// <summary>A line this grammar is required to read.</summary>
    string Witness { get; }

    /// <summary>The first instant this grammar finds, and where.</summary>
    /// <param name="head">The opening of a log.</param>
    (int Offset, DateTime Instant)? FirstIn(string head);
}

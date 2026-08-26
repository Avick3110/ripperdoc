namespace Ripperdoc.Core.Archive;

/// <summary>
/// One resource more than one archive carries, and which of them wins it.
/// </summary>
/// <remarks>
/// Shadowing is whole-file: an archive that loses a resource contributes
/// nothing to it. So a losing carrier is not merged or partially applied - its
/// version of the resource is simply not in the game, and nothing in the
/// install reports that.
/// </remarks>
public sealed class ContestedResource
{
    internal ContestedResource(
        ulong hash,
        string? name,
        IReadOnlyList<ContestCarrier> carriers,
        string? winner,
        IReadOnlyList<string> undeterminedAmong,
        IReadOnlyList<string> shadowed)
    {
        Hash = hash;
        Name = name;
        Carriers = carriers;
        Winner = winner;
        UndeterminedAmong = undeterminedAmong;
        Shadowed = shadowed;
    }

    /// <summary>The identifier the archives address this resource by.</summary>
    public ulong Hash { get; }

    /// <summary>
    /// The resource path, if any carrier's naming produced one; otherwise
    /// <see langword="null" />.
    /// </summary>
    /// <remarks>
    /// Taken from whichever carrier had a name for it, because the same
    /// resource can be named by one archive and nameless in another. A
    /// contested resource nothing can name is still reported - by hash.
    /// </remarks>
    public string? Name { get; }

    /// <summary>Every archive carrying this resource, lowest rank first.</summary>
    public IReadOnlyList<ContestCarrier> Carriers { get; }

    /// <summary>
    /// The archive whose version is in force, or <see langword="null" /> when
    /// this project cannot say which.
    /// </summary>
    public string? Winner { get; }

    /// <summary>
    /// The archives that share the lowest rank, when there is more than one.
    /// </summary>
    /// <remarks>
    /// Empty when there is a winner. Non-empty means the law does not decide
    /// this contest: the leading carriers are archives a present list does not
    /// name, and their order among themselves was never measured. One of these
    /// is winning and this project does not know which - which is a different
    /// statement from either of them winning, and it is the true one.
    /// </remarks>
    public IReadOnlyList<string> UndeterminedAmong { get; }

    /// <summary>
    /// The archives whose version is definitely not in force.
    /// </summary>
    /// <remarks>
    /// Every carrier ranked below the lowest rank, whether or not the winner
    /// among that lowest rank is known. An archive that loses to an unresolved
    /// pair has still lost.
    /// </remarks>
    public IReadOnlyList<string> Shadowed { get; }

    /// <summary>Whether the law names a winner for this contest.</summary>
    public bool HasDeterminedWinner => Winner is not null;

    /// <summary>Whether a naming source could name this resource.</summary>
    public bool IsNamed => !string.IsNullOrEmpty(Name);

    /// <summary>How this resource is written when it is reported.</summary>
    public string Display => ResourceDisplay.Of(Hash, Name);
}

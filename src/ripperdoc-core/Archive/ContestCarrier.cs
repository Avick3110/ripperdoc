namespace Ripperdoc.Core.Archive;

/// <summary>
/// One archive carrying a contested resource, and where it loads.
/// </summary>
/// <param name="FileName">The archive's file name.</param>
/// <param name="Rank">
/// Its load rank. Lower loads first; carriers sharing the lowest rank are
/// carriers this project cannot order.
/// </param>
/// <param name="IsListed">Whether the mod directory's list file names it.</param>
public readonly record struct ContestCarrier(string FileName, int Rank, bool IsListed);

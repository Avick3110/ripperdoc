namespace Ripperdoc.Core.Archive;

/// <summary>
/// What produced an inventory: where it read, and what could name what it
/// found.
/// </summary>
/// <param name="ModDirectory">The directory that was read.</param>
/// <param name="NameSource">
/// The naming posture, as the source described itself.
/// </param>
/// <param name="ResourceLibraryVersion">
/// The version of the pinned library whose index reading produced the entries.
/// </param>
/// <remarks>
/// Naming coverage is a property of the run, not of the archive layer, so the
/// artifact says which posture produced it. Two inventories of one directory
/// under different naming sources disagree about how many entries have names
/// and agree about how many entries exist - and a reader who cannot see the
/// posture has no way to tell that apart from the directory having changed.
/// </remarks>
public readonly record struct InventoryProvenance(
    string ModDirectory,
    string NameSource,
    string ResourceLibraryVersion);

namespace Ripperdoc.Core.Archive;

/// <summary>
/// What produced an inventory: where it read, what could name what it found,
/// and what was actually in force when it read.
/// </summary>
/// <param name="ModDirectory">The directory that was read.</param>
/// <param name="NameSource">
/// The naming source installed for this read, as it described itself. This is
/// what was <em>asked for</em>.
/// </param>
/// <param name="DictionaryLoaded">
/// Whether a resource-name dictionary was observed in the pinned library's
/// resolver at the moment of the read. This is what was <em>in force</em>.
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
/// <para>
/// <strong>Asked for and in force are separate fields because they can
/// differ.</strong> The resolver a dictionary loads into is process-wide and
/// cannot be unloaded, so a read that installed no dictionary still sees one
/// that anything else in the process installed earlier. Recording only the
/// source's own description would then state that no dictionary was installed
/// while the run enjoyed a dictionary's coverage - true about the request,
/// false about the result, and wrong in exactly the direction that makes a
/// coverage figure impossible to interpret.
/// </para>
/// </remarks>
public readonly record struct InventoryProvenance(
    string ModDirectory,
    string NameSource,
    bool DictionaryLoaded,
    string ResourceLibraryVersion);

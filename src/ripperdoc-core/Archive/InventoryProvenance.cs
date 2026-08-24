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
/// and agree about how many entries exist.
/// <para>
/// <strong>What this block does not capture.</strong> The resolver names
/// accumulate in is process-wide and additive, and archives contribute their
/// own declared paths to it as they are read. So a process that has already
/// read other directories can name entries in this one that a fresh process
/// could not, and nothing recorded here distinguishes that from the directory
/// itself having changed. The fields below are what was asked for and what was
/// in force; how much the process had already read is a third influence on the
/// coverage figure and is not recorded.
/// </para>
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

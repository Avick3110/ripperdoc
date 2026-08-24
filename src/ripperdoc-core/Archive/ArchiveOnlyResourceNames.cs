namespace Ripperdoc.Core.Archive;

/// <summary>
/// The engine's default naming posture: whatever the archives name themselves,
/// and nothing else.
/// </summary>
/// <remarks>
/// This source installs nothing and loads nothing, so it cannot fail. That is
/// not the same as naming nothing: an archive that carries its own paths is
/// still read, and its entries are still named. What this posture does not have
/// is the dictionary, so entries whose archive does not name them are reported
/// by hash.
/// <para>
/// The name says <em>archive-only</em> rather than <em>hash-only</em> on
/// purpose. Calling it hash-only would claim the engine names nothing without a
/// dictionary, and that claim is false.
/// </para>
/// </remarks>
public sealed class ArchiveOnlyResourceNames : IResourceNameSource
{
    /// <inheritdoc />
    /// <remarks>
    /// Says what this source does, not what is in force. It cannot claim that
    /// no dictionary is installed, because the resolver a dictionary loads into
    /// is process-wide and something else may already have installed one -
    /// what actually held at the moment of a read is recorded separately, by
    /// observation, in <see cref="InventoryProvenance.DictionaryLoaded" />.
    /// </remarks>
    public string Description => "archive-declared paths; this source installs no name dictionary";

    /// <inheritdoc />
    public void Prepare()
    {
        // Nothing to prepare. Stated rather than left as an empty body, because
        // an empty Prepare() is exactly what a broken source would also have.
    }
}

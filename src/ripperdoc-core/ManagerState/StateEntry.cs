namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// Takes one key and what the file says about it, and decides what to keep.
/// </summary>
/// <param name="key">The key, as its bytes.</param>
/// <param name="sequence">
/// When it was written. Across every file, the highest sequence for a key is
/// the one that stands.
/// </param>
/// <param name="isValue">
/// Whether the entry sets the key or deletes it. A delete makes the key absent
/// rather than present with what it held before.
/// </param>
/// <param name="value">The value, meaningful only where the entry sets one.</param>
/// <remarks>
/// A callback rather than a returned sequence, because it is what lets the
/// decision to copy a value's bytes out of the buffer live in one place. Every
/// key is offered; only the ones the caller keeps are ever materialised.
/// </remarks>
internal delegate void StateEntrySink(
    ReadOnlySpan<byte> key, ulong sequence, bool isValue, ReadOnlySpan<byte> value);
